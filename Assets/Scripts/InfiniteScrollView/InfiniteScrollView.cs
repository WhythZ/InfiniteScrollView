using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//滚动列表样式，只能是垂直或水平其一
public enum ScrollDirection
{
    Vertical,
    Horizontal
}

//垂直样式则表示一行TGrid，水平模式则表示一列TGrid，对象池中存入的是一行或一列的整体而不必然是单个TGrid
public class GridBundle<TGrid> : IPoolObject where TGrid : MonoBehaviour
{
    //持有的列表元素
    public TGrid[] Grids { get; private set; }
    public Vector2 position;

    //若整个无限滚动列表有n个GridBundle，则该参数表示该GridBundle在列表中从1开始的索引，这对于确定当前滚动进度很重要，因为在滑动过程中我们只记录视口内的若干GridBundle
    public int locateIdx;

    //传入该Bundle内的列表元素数量进行构造，注意如果是最后一个Bundle则可能存不满
    public GridBundle(int _goCapacity)
    {
        Grids = new TGrid[_goCapacity];
    }

    //实现IPoolObject要求的函数，用于清空该Bundle状态而回收到对象池中
    public void Reset()
    {
        //无效化定位索引
        locateIdx = -1;
        //隐藏所有列表元素
        foreach (var _grid in Grids)
        {
            if (_grid != null)
                _grid.gameObject.SetActive(false);
        }
    }
}

//通过继承并实现此抽象类来使用滚动列表功能（修饰partial表示该类的定义可以被拆分到多个文件中）
//TGrid传入滚动列表元素的MonoBehaviour子类类型（对应类脚本挂载在元素预制体上），TGridData传入TGrid对应的数据存储结构体类型
public abstract partial class InfiniteScrollView<TGrid, TGridData> : MonoBehaviour where TGrid : MonoBehaviour
{
    #region Settings
    [SerializeField] private ScrollDirection scrollDirection; //滚动方向
    [SerializeField] protected TGrid gridPrefab;              //挂载TGrid类型脚本的列表元素预制体
    [SerializeField] protected RectTransform contentRect;     //对象Viewport下存放列表元素的子对象
    [SerializeField] private Vector2 gridSpacing;             //设置Grid间的水平或垂直方向上的间距
    [SerializeField] private int rowOrColCount;               //垂直则表每行数量，水平则为每列数量
    private RectTransform parentRect;                         //自动获取持有ScrollRect的那个父对象
    #endregion

    #region Grid
    //维护列表元素数据，而元素对象则间接通过GridBundle由对象池动态管理
    public ICollection<TGridData> GridDatas { get; private set; }

    //anchorMin和anchorMax分别表示左下角和右上角锚点，x和y的取值范围为0~1，这两点定义了一个锚点区域，UI元素会相对于该区域进行定位和大小计算
    //若两者均为Vector2(0.5f,0.5f)而重合，则UI元素固定在父容器的中心点，可通过anchoredPosition调整相对位置，通过rect或sizeDelta控制大小
    //若anchorMin为Vector2(0,0.5f)即左中、anchorMax为Vector2(1,0.5f)即右中，即锚点水平方向分开、垂直方向重合，则该UI元素会水平拉伸适应父UI而垂直固定
    //若anchorMin为Vector2(0.5f,0)即中下、anchorMax为Vector2(0.5f,1)即中上，即锚点垂直方向分开、水平方向重合，则该UI元素会垂直拉伸适应父UI而水平固定
    //若anchorMin为Vector2(0,0)即左下、anchorMax为Vector2(1,1)即右上，则该UI元素会完全拉伸适应父UI的宽高达到四边贴合的效果
    //RectTransform.rect的width/height表示UI元素实际的世界空间尺寸
    //RectTransform.sizeDelta的x/y表示UI元素相对于锚点参考尺寸的偏移
    //rect.width = 父对象宽度 * (anchorMax.x - anchorMin.x) + sizeDelta.x
    //rect.height = 父对象高度 * (anchorMax.y - anchorMin.y) + sizeDelta.y
    public Vector2 GridSize => gridPrefab.GetComponent<RectTransform>().sizeDelta; //获取单个列表元素的尺寸
    public Vector2 GridSlotSize => GridSize + gridSpacing; //获取单个列表元素的含间距占位尺寸
    #endregion

    #region GridBundle
    //只负责存储视口范围内的GridBundle的链表，其总数量<=逻辑上GridBundle的总数量，由对象池动态管理
    private readonly LinkedList<GridBundle<TGrid>> visibleGridBundles = new LinkedList<GridBundle<TGrid>>();
    //获取逻辑上GridBundle的总数量
    public int LogicalGridBundleCount
    {
        get
        {
            int _gridCount = GridDatas.Count;
            return (_gridCount % rowOrColCount == 0) ? (_gridCount / rowOrColCount) : (_gridCount / rowOrColCount + 1);
        }
    }
    #endregion

    private void Awake()
    {
        //获取挂载对象的RectTransform组件（该对象应当是拥有ScrollRect组件的那个父对象）
        parentRect = GetComponent<RectTransform>();
    }

    //初始化以及数据长度发生变化时，需调用该函数更新
    public virtual void Initialize(ICollection<TGridData> _datas, bool _resetScrollProgess = false)
    {
        if (_datas == null)
        {
            Debug.LogError("InfiniteScrollView.Initialize接收的数据源为空");
            return;
        }

        //应用外部传入的数据源
        GridDatas = _datas;
        //根据数据源重新计算Content的尺寸
        RecalculateContentSize(_resetScrollProgess);

        //清除头部和尾部
        UpdateDisplay();

        //刷新视口内所有元素的视图
        RefreshVisibleGridBundlesView();
    }

    //数据的长度发生变化的时候使用这个函数刷新一下
    public void Refresh(bool _resetContentPos = false)
    {
        RecalculateContentSize(_resetContentPos);
        contentRect.anchoredPosition = _resetContentPos ? Vector2.zero : contentRect.anchoredPosition;
        UpdateDisplay();
        RefreshVisibleGridBundlesView();
    }

    protected virtual void Update()
    {
        if (GridDatas == null)
            return;
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        RemoveHead();
        RemoveTail();
        if (visibleGridBundles.Count == 0)
            RefreshAllCellInViewRange();
        else
        {
            AddHead();
            AddTail();
        }
        //清除越界，比如数据减少，此时就要清理在视野内但在数据之外的UI
        RemoveItemOutOfListRange();
    }

    public void RefreshAllCellInViewRange()
    {
        int itemCount = LogicalGridBundleCount;
        Vector2 viewRangeSize = parentRect.sizeDelta;
        Vector2 itemSize = GridSlotSize;
        Vector2 cellSize = GridSize;
        Vector2 cellSpace = gridSpacing;

        if (scrollDirection == ScrollDirection.Vertical)
        {
            Vector2 topPos = -contentRect.anchoredPosition;
            Vector2 bottomPos = new Vector2(topPos.x, topPos.y - viewRangeSize.y);
            int startIndex = GetIndex(topPos);
            int endIndex = GetIndex(bottomPos);
            for (int i = startIndex; i <= endIndex && i < itemCount; i++)
            {
                Vector2 pos = new Vector2(contentRect.anchoredPosition.x, -i * itemSize.y);
                var bundle = GetGridBundle(i, pos, cellSize, cellSpace);
                visibleGridBundles.AddLast(bundle);
            }
        }
        else if (scrollDirection == ScrollDirection.Horizontal)
        {
            Vector2 leftPos = -contentRect.anchoredPosition;
            Vector2 rightPos = new Vector2(leftPos.x + viewRangeSize.x, leftPos.y);

            int startIndex = GetIndex(leftPos);
            int endIndex = GetIndex(rightPos);

            for (int i = startIndex; i <= endIndex && i < itemCount; i++)
            {
                Vector2 pos = new Vector2(i * itemSize.x, contentRect.anchoredPosition.y);
                var bundle = GetGridBundle(i, pos, cellSize, cellSpace);
                visibleGridBundles.AddLast(bundle);
            }
        }
    }

    private void AddHead()
    {
        //以头部元素向外计算出新头部的位置,计算该位置是否在显示区域，如果在显示区域则生成对应项目
        GridBundle<TGrid> bundle = visibleGridBundles.First.Value;

        Vector2 offset = default;
        if (scrollDirection == ScrollDirection.Vertical)
            offset = new Vector2(0, GridSlotSize.y);
        else if (scrollDirection == ScrollDirection.Horizontal)
            offset = new Vector2(-GridSlotSize.x, 0);

        Vector2 newHeadBundlePos = bundle.position + offset;

        while (IsInViewport(newHeadBundlePos))
        {
            int caculatedIndex = GetIndex(newHeadBundlePos);
            int index = bundle.locateIdx - 1;

            if (index < 0) break;
            if (caculatedIndex != index)
                Debug.LogError($"计算索引:{caculatedIndex},计数索引{index}计算出的索引和计数的索引值不相等...");

            bundle = GetGridBundle(index, newHeadBundlePos, GridSize, gridSpacing);
            visibleGridBundles.AddFirst(bundle);

            newHeadBundlePos = bundle.position + offset;
        }
    }

    private void RemoveHead()
    {
        if (visibleGridBundles.Count == 0)
            return;

        if (scrollDirection == ScrollDirection.Vertical)
        {
            GridBundle<TGrid> bundle = visibleGridBundles.First.Value;
            while (IsAboveViewport(bundle.position))
            {
                //进入对象池
                ReleaseGridBundle(bundle);
                visibleGridBundles.RemoveFirst();

                if (visibleGridBundles.Count == 0) break;

                bundle = visibleGridBundles.First.Value;
            }
        }
        else if (scrollDirection == ScrollDirection.Horizontal)
        {
            GridBundle<TGrid> bundle = visibleGridBundles.First.Value;
            while (IsInViewportLeft(bundle.position))
            {
                //进入对象池
                ReleaseGridBundle(bundle);
                visibleGridBundles.RemoveFirst();

                if (visibleGridBundles.Count == 0) break;

                bundle = visibleGridBundles.First.Value;
            }
        }
    }

    private void AddTail()
    {
        //以尾部元素向外计算出新头部的位置,计算该位置是否在显示区域，如果在显示区域则生成对应项目
        GridBundle<TGrid> bundle = visibleGridBundles.Last.Value;
        Vector2 offset = default;
        if (scrollDirection == ScrollDirection.Vertical)
            offset = new Vector2(0, -GridSlotSize.y);
        else if (scrollDirection == ScrollDirection.Horizontal)
            offset = new Vector2(GridSlotSize.x, 0);

        Vector2 newTailBundlePos = bundle.position + offset;

        while (IsInViewport(newTailBundlePos))
        {
            int caculatedIndex = GetIndex(newTailBundlePos);
            int index = bundle.locateIdx + 1;

            if (index >= LogicalGridBundleCount) break;
            if (caculatedIndex != index)
                Debug.LogError($"计算索引:{caculatedIndex},计数索引{index}计算出的索引和计数的索引值不相等...");

            bundle = GetGridBundle(index, newTailBundlePos, GridSize, gridSpacing);
            visibleGridBundles.AddLast(bundle);

            newTailBundlePos = bundle.position + offset;
        }
    }

    private void RemoveTail()
    {
        if (visibleGridBundles.Count == 0)
            return;

        if (scrollDirection == ScrollDirection.Vertical)
        {
            GridBundle<TGrid> bundle = visibleGridBundles.Last.Value;
            while (IsBelowViewport(bundle.position))
            {
                //进入对象池
                ReleaseGridBundle(bundle);
                visibleGridBundles.RemoveLast();

                if (visibleGridBundles.Count == 0) break;

                bundle = visibleGridBundles.Last.Value;
            }
        }
        else if (scrollDirection == ScrollDirection.Horizontal)
        {
            GridBundle<TGrid> bundle = visibleGridBundles.Last.Value;
            while (IsInViewportRight(bundle.position))
            {
                //进入对象池
                ReleaseGridBundle(bundle);
                visibleGridBundles.RemoveLast();

                if (visibleGridBundles.Count == 0) break;

                bundle = visibleGridBundles.Last.Value;
            }
        }
    }

    private void RemoveItemOutOfListRange()
    {
        if (visibleGridBundles.Count() == 0)
            return;
        var bundle = visibleGridBundles.Last.Value;
        int lastItemIndex = LogicalGridBundleCount - 1;
        while (bundle.locateIdx > lastItemIndex && visibleGridBundles.Count() > 0)
        {
            visibleGridBundles.RemoveLast();
            ReleaseGridBundle(bundle);
        }
    }

    public virtual Vector2 CaculateRelativePostion(Vector2 _curPosition)
    {
        Vector2 _relativePosition = default;
        if (scrollDirection == ScrollDirection.Horizontal)
            _relativePosition = new Vector2(_curPosition.x + contentRect.anchoredPosition.x, _curPosition.y);
        else if (scrollDirection == ScrollDirection.Vertical)
            _relativePosition = new Vector2(_curPosition.x, _curPosition.y + contentRect.anchoredPosition.y);
        return _relativePosition;
    }

    public int GetIndex(Vector2 _position)
    {
        int index = -1;
        if (scrollDirection == ScrollDirection.Vertical)
        {
            index = Mathf.RoundToInt(-_position.y / GridSlotSize.y);
            return index;
        }
        else if (scrollDirection == ScrollDirection.Horizontal)
        {
            index = Mathf.RoundToInt(_position.x / GridSlotSize.x);
        }
        return index;
    }

    #region CheckViewportRelation
    public bool IsAboveViewport(Vector2 _position)
    {
        Vector2 _relativePos = CaculateRelativePostion(_position);
        return _relativePos.y > GridSlotSize.y;
    }

    public bool IsBelowViewport(Vector2 _position)
    {
        Vector2 _relativePos = CaculateRelativePostion(_position);
        return _relativePos.y < -parentRect.sizeDelta.y;
    }

    public bool IsInViewportLeft(Vector2 _position)
    {
        Vector2 _relativePos = CaculateRelativePostion(_position);
        return _relativePos.x < -GridSlotSize.x;
    }

    public bool IsInViewportRight(Vector2 _position)
    {
        Vector2 _relativePos = CaculateRelativePostion(_position);
        return _relativePos.x > parentRect.sizeDelta.x;
    }

    public bool IsInViewport(Vector2 _position)
    {
        if (scrollDirection == ScrollDirection.Horizontal)
            return !IsInViewportLeft(_position) && !IsInViewportRight(_position);
        else if (scrollDirection == ScrollDirection.Vertical)
            return !IsAboveViewport(_position) && !IsBelowViewport(_position);
        return false;
    }
    #endregion
}