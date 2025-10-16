using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//滚动列表样式，垂直或水平
public enum ScrollDirection
{
    Vertical,
    Horizontal
}

//垂直样式则表示一行TGrid，水平模式则表示一列TGrid，对象池中存入的是一行或一列的整体而不必然是单个TGrid
public class GridBundle<TGrid> : IPoolObject where TGrid : MonoBehaviour
{
    public TGrid[] Grids { get; private set; }
    public int index;
    public Vector2 position;

    //传入行或列数量进行构造
    public GridBundle(int _goCapacity)
    {
        Grids = new TGrid[_goCapacity];
    }

    //隐藏该行或列的所有元素
    public void HideAllGrids()
    {
        index = -1;
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
    [SerializeField] private ScrollDirection scrollDir;
    [SerializeField] protected TGrid gridPrefab;          //挂载TGrid类型脚本的列表元素预制体
    [SerializeField] protected RectTransform contentRect; //对象Viewport下存放列表元素的子对象
    [SerializeField] private Vector2 gridSpacing;         //设置Grid间的水平或垂直方向上的间距
    [SerializeField] private int rowOrColCount;           //垂直则表每行数量，水平则为每列数量
    private RectTransform parentRect;                     //自动获取持有ScrollRect的那个父对象

    public ICollection<TGridData> Datas { get; private set; }

    public Vector2 contentPos => contentRect.position;
    public Vector2 contentSize => contentRect.sizeDelta;
    public Vector2 gridSize => gridRectTransform.sizeDelta;
    public Vector2 slotSize => gridSize + gridSpacing;

    private RectTransform gridRectTransform;
    private readonly Vector2 horizontalContentAnchorMin = new Vector2(0, 0);
    private readonly Vector2 horizontalContentAnchorMax = new Vector2(0, 1);
    private readonly Vector2 verticalContentAnchorMin = new Vector2(0, 1);
    private readonly Vector2 verticalContentAnchorMax = new Vector2(1, 1);
    private readonly LinkedList<GridBundle<TGrid>> gridBundles = new LinkedList<GridBundle<TGrid>>();

    public int ItemCount
    {
        get
        {
            int _gridCount = Datas.Count;
            return (_gridCount % rowOrColCount == 0) ? (_gridCount / rowOrColCount) : (_gridCount / rowOrColCount + 1);
        }
    }

    private void Awake()
    {
        //获取挂载对象的RectTransform组件（该对象应当是拥有ScrollRect组件的那个父对象）
        parentRect = GetComponent<RectTransform>();
    }

    //Scroll的初始化和数据的长度发生变化都需要使用这个函数，该函数只涉及Content的大小变化
    public virtual void Initialize(ICollection<TGridData> _datas, bool _resetPos = false)
    {
        if (_datas == null)
            throw new System.Exception("InfiniteScrollView.Initialize接收的数据为空");

        gridRectTransform = gridPrefab.GetComponent<RectTransform>();
        Datas = _datas;
        RecalculateContentSize(_resetPos);

        //清除头部和尾部
        UpdateDisplay();
        RefrashViewRangeData();
    }

    //数据的长度发生变化的时候使用这个函数刷新一下
    public void Refresh(bool _resetContentPos = false)
    {
        RecalculateContentSize(_resetContentPos);
        contentRect.anchoredPosition = _resetContentPos ? Vector2.zero : contentRect.anchoredPosition;
        UpdateDisplay();
        RefrashViewRangeData();
    }

    protected virtual void Update()
    {
        if (Datas == null)
            return;
        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        RemoveHead();
        RemoveTail();
        if (gridBundles.Count == 0)
        {
            RefreshAllCellInViewRange();
        }
        else
        {
            AddHead();
            AddTail();
        }
        //清除越界,比如数据减少,此时就要清理在视野内的,在数据之外的UI
        RemoveItemOutOfListRange();
    }

    public void RefreshAllCellInViewRange()
    {
        int itemCount = ItemCount;
        Vector2 viewRangeSize = parentRect.sizeDelta;
        Vector2 itemSize = slotSize;
        Vector2 cellSize = gridSize;
        Vector2 cellSpace = gridSpacing;

        if (scrollDir == ScrollDirection.Vertical)
        {
            Vector2 topPos = -contentRect.anchoredPosition;
            Vector2 bottomPos = new Vector2(topPos.x, topPos.y - viewRangeSize.y);
            int startIndex = GetIndex(topPos);
            int endIndex = GetIndex(bottomPos);
            for (int i = startIndex; i <= endIndex && i < itemCount; i++)
            {
                Vector2 pos = new Vector2(contentRect.anchoredPosition.x, -i * itemSize.y);
                var bundle = GetGridBundle(i, pos, cellSize, cellSpace);
                gridBundles.AddLast(bundle);
            }
        }
        else if (scrollDir == ScrollDirection.Horizontal)
        {
            Vector2 leftPos = -contentRect.anchoredPosition;
            Vector2 rightPos = new Vector2(leftPos.x + viewRangeSize.x, leftPos.y);

            int startIndex = GetIndex(leftPos);
            int endIndex = GetIndex(rightPos);

            for (int i = startIndex; i <= endIndex && i < itemCount; i++)
            {
                Vector2 pos = new Vector2(i * itemSize.x, contentRect.anchoredPosition.y);
                var bundle = GetGridBundle(i, pos, cellSize, cellSpace);
                gridBundles.AddLast(bundle);
            }
        }
    }

    private void AddHead()
    {
        //以头部元素向外计算出新头部的位置,计算该位置是否在显示区域，如果在显示区域则生成对应项目
        GridBundle<TGrid> bundle = gridBundles.First.Value;

        Vector2 offset = default;
        if (scrollDir == ScrollDirection.Vertical)
            offset = new Vector2(0, slotSize.y);
        else if (scrollDir == ScrollDirection.Horizontal)
            offset = new Vector2(-slotSize.x, 0);

        Vector2 newHeadBundlePos = bundle.position + offset;

        while (OnViewRange(newHeadBundlePos))
        {
            int caculatedIndex = GetIndex(newHeadBundlePos);
            int index = bundle.index - 1;

            if (index < 0) break;
            if (caculatedIndex != index)
                Debug.LogError($"计算索引:{caculatedIndex},计数索引{index}计算出的索引和计数的索引值不相等...");

            bundle = GetGridBundle(index, newHeadBundlePos, gridSize, gridSpacing);
            gridBundles.AddFirst(bundle);

            newHeadBundlePos = bundle.position + offset;
        }
    }

    private void RemoveHead()
    {
        if (gridBundles.Count == 0)
            return;

        if (scrollDir == ScrollDirection.Vertical)
        {
            GridBundle<TGrid> bundle = gridBundles.First.Value;
            while (AboveViewRange(bundle.position))
            {
                //进入对象池
                ReleaseGridBundle(bundle);
                gridBundles.RemoveFirst();

                if (gridBundles.Count == 0) break;

                bundle = gridBundles.First.Value;
            }
        }
        else if (scrollDir == ScrollDirection.Horizontal)
        {
            GridBundle<TGrid> bundle = gridBundles.First.Value;
            while (InViewRangeLeft(bundle.position))
            {
                //进入对象池
                ReleaseGridBundle(bundle);
                gridBundles.RemoveFirst();

                if (gridBundles.Count == 0) break;

                bundle = gridBundles.First.Value;
            }
        }
    }

    private void AddTail()
    {
        //以尾部元素向外计算出新头部的位置,计算该位置是否在显示区域，如果在显示区域则生成对应项目
        GridBundle<TGrid> bundle = gridBundles.Last.Value;
        Vector2 offset = default;
        if (scrollDir == ScrollDirection.Vertical)
            offset = new Vector2(0, -slotSize.y);
        else if (scrollDir == ScrollDirection.Horizontal)
            offset = new Vector2(slotSize.x, 0);

        Vector2 newTailBundlePos = bundle.position + offset;

        while (OnViewRange(newTailBundlePos))
        {
            int caculatedIndex = GetIndex(newTailBundlePos);
            int index = bundle.index + 1;

            if (index >= ItemCount) break;
            if (caculatedIndex != index)
                Debug.LogError($"计算索引:{caculatedIndex},计数索引{index}计算出的索引和计数的索引值不相等...");

            bundle = GetGridBundle(index, newTailBundlePos, gridSize, gridSpacing);
            gridBundles.AddLast(bundle);

            newTailBundlePos = bundle.position + offset;
        }
    }

    private void RemoveTail()
    {
        if (gridBundles.Count == 0)
            return;

        if (scrollDir == ScrollDirection.Vertical)
        {
            GridBundle<TGrid> bundle = gridBundles.Last.Value;
            while (UnderViewRange(bundle.position))
            {
                //进入对象池
                ReleaseGridBundle(bundle);
                gridBundles.RemoveLast();

                if (gridBundles.Count == 0) break;

                bundle = gridBundles.Last.Value;
            }
        }
        else if (scrollDir == ScrollDirection.Horizontal)
        {
            GridBundle<TGrid> bundle = gridBundles.Last.Value;
            while (InViewRangeRight(bundle.position))
            {
                //进入对象池
                ReleaseGridBundle(bundle);
                gridBundles.RemoveLast();

                if (gridBundles.Count == 0) break;

                bundle = gridBundles.Last.Value;
            }
        }
    }

    private void RemoveItemOutOfListRange()
    {
        if (gridBundles.Count() == 0)
            return;
        var bundle = gridBundles.Last.Value;
        int lastItemIndex = ItemCount - 1;
        while (bundle.index > lastItemIndex && gridBundles.Count() > 0)
        {
            gridBundles.RemoveLast();
            ReleaseGridBundle(bundle);
        }
    }

    public virtual Vector2 CaculateRelativePostion(Vector2 curPosition)
    {
        Vector2 relativePosition = default;
        if (scrollDir == ScrollDirection.Horizontal)
        {
            relativePosition = new Vector2(curPosition.x + contentRect.anchoredPosition.x, curPosition.y);
        }
        else if (scrollDir == ScrollDirection.Vertical)
        {
            relativePosition = new Vector2(curPosition.x, curPosition.y + contentRect.anchoredPosition.y);
        }
        return relativePosition;
    }

    public int GetIndex(Vector2 position)
    {
        int index = -1;
        if (scrollDir == ScrollDirection.Vertical)
        {
            index = Mathf.RoundToInt(-position.y / slotSize.y);
            return index;
        }
        else if (scrollDir == ScrollDirection.Horizontal)
        {
            index = Mathf.RoundToInt(position.x / slotSize.x);
        }
        return index;
    }

    public bool AboveViewRange(Vector2 position)
    {
        Vector2 relativePos = CaculateRelativePostion(position);
        return relativePos.y > slotSize.y;
    }

    public bool UnderViewRange(Vector2 position)
    {
        Vector2 relativePos = CaculateRelativePostion(position);
        return relativePos.y < -parentRect.sizeDelta.y;
    }

    public bool InViewRangeLeft(Vector2 position)
    {
        Vector2 relativePos = CaculateRelativePostion(position);
        return relativePos.x < -slotSize.x;
    }

    public bool InViewRangeRight(Vector2 position)
    {
        Vector2 relativePos = CaculateRelativePostion(position);
        return relativePos.x > parentRect.sizeDelta.x;
    }

    public bool OnViewRange(Vector2 position)
    {
        if (scrollDir == ScrollDirection.Horizontal)
        {
            return !InViewRangeLeft(position) && !InViewRangeRight(position);
        }
        else if (scrollDir == ScrollDirection.Vertical)
        {
            return !AboveViewRange(position) && !UnderViewRange(position);
        }
        return false;
    }
}