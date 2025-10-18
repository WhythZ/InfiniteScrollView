using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

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
    
    //该属性意义相当于该Bundle头部Grid的左上角位置，也可视作Bundle整体的左上角位置
    public Vector2 leftTopPosition;

    //若整个无限滚动列表有n个GridBundle，则该参数表示该GridBundle在列表中的编号索引，取值范围是[0,n-1]
    //这对于确定当前滚动进度很重要，因为在滑动过程中我们只记录视口内的若干GridBundle
    public int locateIdx;

    //传入该Bundle内的列表元素数量进行构造，注意如果是最后一个Bundle则可能存不满
    public GridBundle(int _goCapacity)
    {
        Grids = new TGrid[_goCapacity];
    }

    //实现IPoolObject要求的函数，用于清空该Bundle状态而回收到对象池中
    public void ResetPoolObject()
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
    [SerializeField] private RectTransform viewportRect;      //自动获取持有ScrollRect的那个父对象
    [SerializeField] protected RectTransform contentRect;     //对象Viewport下存放列表元素的子对象
    [SerializeField] private Vector2 gridSpacing;             //设置Grid间的水平或垂直方向上的间距
    [SerializeField] private int rowOrColCount;               //垂直则表每行数量，水平则为每列数量
    #endregion

    #region GridAndBundle
    //维护列表元素数据，而元素对象则间接通过GridBundle由对象池动态管理
    public ICollection<TGridData> GridDatas { get; private set; }
    //注意只有在两锚点重合时，sizeDelta.x/y才正好和rect.width/height相等，最好不要使用sizeDelta
    //sizeDelta.x = rect.width - 父对象宽度* (anchorMax.x - anchorMin.x)
    //sizeDelta.y = rect.height - 父对象高度* (anchorMax.y - anchorMin.y)
    public Vector2 GridSize => new Vector2(gridPrefab.GetComponent<RectTransform>().rect.width, gridPrefab.GetComponent<RectTransform>().rect.height); //获取单个列表元素的尺寸
    public Vector2 GridSlotSize => GridSize + gridSpacing; //获取单个列表元素的含间距占位尺寸

    //只负责存储视口范围内的GridBundle的链表，其总数量<=逻辑上GridBundle的总数量，由对象池动态管理
    private readonly LinkedList<GridBundle<TGrid>> visibleGridBundles = new LinkedList<GridBundle<TGrid>>();
    //获取逻辑上GridBundle的总数量，包含不在视口范围内的其它GridBundle
    public int LogicalGridBundleCount
    {
        get
        {
            int _gridCount = GridDatas.Count;
            return (_gridCount % rowOrColCount == 0) ? (_gridCount / rowOrColCount) : (_gridCount / rowOrColCount + 1);
        }
    }
    #endregion

    //初始化以及数据长度发生变化时，需调用该函数更新
    public virtual void Initialize(ICollection<TGridData> _datas, bool _resetScrollProgess = false)
    {
        if (_datas == null)
        {
            Debug.LogError("InfiniteScrollView.Initialize接收的数据源为空");
            return;
        }

        //应用外部传入的数据源，然后根据数据源重新计算Content的尺寸
        GridDatas = _datas;
        RecalculateContentSize(_resetScrollProgess);

        //刷新视口内所有元素的视图
        RefreshVisibleGridBundlesView();
    }

    #region UpdateVisibleBundles
    protected virtual void Update()
    {
        //滚动视图数据还未初始化
        if (GridDatas == null) return;

        //需先初始化视口内Bundle
        if (visibleGridBundles.Count == 0)
            InitVisibleGridBundles();
        else
        {
            //更新首尾
            AddHead();
            AddTail();
            RemoveHead();
            RemoveTail();
        }

        //移除非法Bundle
        RemoveInvalidBundles();
    }

    //根据传入的上边界y值或左边界x值计算Bundle的定位索引
    private int GetGridBundleLocateIdx(Vector2 _bundlePosition)
    {
        //对于垂直滚动，有用的是传入的Bundle的上边界y值，x值无意义
        if (scrollDirection == ScrollDirection.Vertical)
            return Mathf.RoundToInt(-_bundlePosition.y / GridSlotSize.y);
        //对于水平滚动，有用的是传入的Bundle的左边界x值，y值无意义
        else if (scrollDirection == ScrollDirection.Horizontal)
            return Mathf.RoundToInt(_bundlePosition.x / GridSlotSize.x);
        return -1;
    }

    //初始化visibleGridBundles即视口内的Bundle
    private void InitVisibleGridBundles()
    {
        if (scrollDirection == ScrollDirection.Vertical)
        {
            //计算视口切割Content的上下边界，以此计算视口内Bundle的定位索引，以便初始化visibleGridBundles内的Bundle
            //垂直滚动的Content锚框水平拉伸，垂直则向Viewport上侧对齐，故此处-anchoredPosition.y即Content上边界向上偏移Viewport上边界的距离（等价于Viewport上边界以Content上边界为基准的向下偏移距离）
            Vector2 _upCuttingBound = new Vector2(0, -contentRect.anchoredPosition.y);
            Vector2 _downCuttingBound = new Vector2(0, _upCuttingBound.y - viewportRect.rect.height);

            //初始化视口内Bundle
            for (int _iBundleLocateIdx = GetGridBundleLocateIdx(_upCuttingBound); _iBundleLocateIdx <= GetGridBundleLocateIdx(_downCuttingBound) && _iBundleLocateIdx < LogicalGridBundleCount; _iBundleLocateIdx++)
            {
                //Content轴心在自身左上角，此处计算Bundle相对Content轴心的位置
                GridBundle<TGrid> _newBundle = GetGridBundle(_iBundleLocateIdx, new Vector2(0, -_iBundleLocateIdx * GridSlotSize.y));
                visibleGridBundles.AddLast(_newBundle);
            }
        }
        else if (scrollDirection == ScrollDirection.Horizontal)
        {
            //计算视口切割Content的左右边界，以此计算视口内Bundle的定位索引，以便初始化visibleGridBundles内的Bundle
            //水平滚动的Content锚框垂直拉伸，水平则向Viewport左侧对齐，故此处-anchoredPosition.x即Content上边界向左偏移Viewport左边界的距离（等价于Viewport左边界以Content左边界为基准的向右偏移距离）
            Vector2 _leftCuttingBound = new Vector2(-contentRect.anchoredPosition.x, 0);
            Vector2 _rightCuttingBound = new Vector2(_leftCuttingBound.x + viewportRect.rect.width, 0);

            //初始化视口内Bundle
            for (int _iBundleLocateIdx = GetGridBundleLocateIdx(_leftCuttingBound); _iBundleLocateIdx <= GetGridBundleLocateIdx(_rightCuttingBound) && _iBundleLocateIdx < LogicalGridBundleCount; _iBundleLocateIdx++)
            {
                //Content轴心在自身左上角，此处计算Bundle相对Content轴心的位置
                var _newBundle = GetGridBundle(_iBundleLocateIdx, new Vector2(_iBundleLocateIdx * GridSlotSize.x, 0));
                visibleGridBundles.AddLast(_newBundle);
            }
        }
    }

    //垂直向上滚动或水平向左滚动时，显示上侧或左侧的范围内Bundle
    private void AddHead()
    {
        //防止还未初始化
        if (visibleGridBundles.First == null) return;
        //以头部Bundle元素向外计算出新头部的位置，若新头部也在视口内则添加该新头部Bundle
        GridBundle<TGrid> _oldHeadBundle = visibleGridBundles.First.Value;

        Vector2 _expandOffset = default;
        if (scrollDirection == ScrollDirection.Vertical)
            _expandOffset = new Vector2(0, GridSlotSize.y);
        else if (scrollDirection == ScrollDirection.Horizontal)
            _expandOffset = new Vector2(-GridSlotSize.x, 0);

        //循环直到添加完所有范围内的Bundle
        Vector2 _newHeadBundleLeftTopPos = _oldHeadBundle.leftTopPosition + _expandOffset;
        while (IsInViewport(_newHeadBundleLeftTopPos))
        {
            //或用GetGridBundleLocateIdx(_newTailBundleLeftTopPos)计算所得结果理应相同，若超出数据范围则结束添加
            int _newHeadBundleLocateIdx = _oldHeadBundle.locateIdx - 1;
            if (_newHeadBundleLocateIdx < 0) break;

            //若确定添加则从对象池中取出Bundle给新头部使用，当前被添加的新头部Bundle就是下一轮的旧头部（此处_oldHeadBundle只是个值而不是引用）
            _oldHeadBundle = GetGridBundle(_newHeadBundleLocateIdx, _newHeadBundleLeftTopPos);
            visibleGridBundles.AddFirst(_oldHeadBundle); //存入双向链表中
            //继续延伸新头部进行下一轮检测
            _newHeadBundleLeftTopPos = _oldHeadBundle.leftTopPosition + _expandOffset;
        }
    }

    //垂直向下滚动或水平向右滚动时，显示下侧或右侧的范围内Bundle
    private void AddTail()
    {
        //防止还未初始化
        if (visibleGridBundles.Last == null) return;
        //以尾部Bundle元素向外计算出新尾部的位置，若新尾部也在视口内则添加该新尾部Bundle
        GridBundle<TGrid> _oldTailBundle = visibleGridBundles.Last.Value;

        Vector2 _expandOffset = default;
        if (scrollDirection == ScrollDirection.Vertical)
            _expandOffset = new Vector2(0, -GridSlotSize.y); //向下延伸
        else if (scrollDirection == ScrollDirection.Horizontal)
            _expandOffset = new Vector2(GridSlotSize.x, 0); //向右延伸

        //循环直到添加完所有范围内的Bundle
        Vector2 _newTailBundleLeftTopPos = _oldTailBundle.leftTopPosition + _expandOffset;
        while (IsInViewport(_newTailBundleLeftTopPos))
        {
            //或用GetGridBundleLocateIdx(_newTailBundleLeftTopPos)计算所得结果理应相同，若超出数据范围则结束添加
            int _newTailBundleLocateIdx = _oldTailBundle.locateIdx + 1;
            if (_newTailBundleLocateIdx < 0) break;

            //若确定添加则从对象池中取出Bundle给新头部使用，当前被添加的新头部Bundle就是下一轮的旧头部（此处_oldHeadBundle只是个值而不是引用）
            _oldTailBundle = GetGridBundle(_newTailBundleLocateIdx, _newTailBundleLeftTopPos);
            visibleGridBundles.AddLast(_oldTailBundle); //存入双向链表中
            //继续延伸新头部进行下一轮检测
            _newTailBundleLeftTopPos = _oldTailBundle.leftTopPosition + _expandOffset;
        }
    }

    //垂直向下滚动或水平向右滚动时，隐藏上侧或左侧的超范围Bundle
    private void RemoveHead()
    {
        if (visibleGridBundles.Count == 0) return;

        //防止还未初始化
        if (visibleGridBundles.First == null) return;
        //循环直到清除所有超范围的Bundle
        GridBundle<TGrid> _headBundle = visibleGridBundles.First.Value;
        while (!IsInViewport(_headBundle.leftTopPosition))
        {
            //Debug.LogError(gameObject.name + "RemoveHead" + _headBundle.locateIdx);
            //回收入对象池
            ReleaseGridBundle(_headBundle);
            visibleGridBundles.RemoveFirst();
            //终止或进入下一轮检测
            if (visibleGridBundles.Count == 0)
                break;
            else
                _headBundle = visibleGridBundles.First.Value;
        }
    }

    //垂直向上滚动或水平向左滚动时，隐藏下侧或右侧的超范围Bundle
    private void RemoveTail()
    {
        if (visibleGridBundles.Count == 0) return;

        //防止还未初始化
        if (visibleGridBundles.Last == null) return;
        //循环直到清除所有超范围的Bundle
        GridBundle<TGrid> _tailBundle = visibleGridBundles.Last.Value;
        while (!IsInViewport(_tailBundle.leftTopPosition))
        {
            //Debug.LogError(gameObject.name + "RemoveTail" + _tailBundle.locateIdx);
            //回收入对象池
            ReleaseGridBundle(_tailBundle);
            visibleGridBundles.RemoveLast();
            //终止或进入下一轮检测
            if (visibleGridBundles.Count == 0)
                break;
            else
                _tailBundle = visibleGridBundles.Last.Value;
        }
    }

    //移除视口中可能存在的的非法Bundle（例如将数据量删除到很少，以至于连塞满视口都不够时）
    private void RemoveInvalidBundles()
    {
        if (visibleGridBundles.Count == 0)
            return;

        //排除非法数据
        GridBundle<TGrid> _headBundle = visibleGridBundles.First.Value;
        while (_headBundle.locateIdx < 0 && visibleGridBundles.Count > 0)
        {
            visibleGridBundles.RemoveFirst();
            ReleaseGridBundle(_headBundle);
        }
        GridBundle<TGrid> _tailBundle = visibleGridBundles.Last.Value;
        while (_tailBundle.locateIdx > LogicalGridBundleCount - 1 && visibleGridBundles.Count > 0)
        {
            visibleGridBundles.RemoveLast();
            ReleaseGridBundle(_tailBundle);
        }
    }
    #endregion

    #region CheckInViewport
    //由于Bundle的leftTopPosition以Content轴心（设置在左上角）作为原点，故此处需换算Bundle整体左上角点位相对父对象即Viewport的偏移
    private Vector2 CaculateGridBundleOffsetToViewport(Vector2 _leftTopPosition)
    {
        Vector2 _offsetToViewport = default;
        if (scrollDirection == ScrollDirection.Vertical)
            _offsetToViewport = new Vector2(_leftTopPosition.x, contentRect.anchoredPosition.y + _leftTopPosition.y);
        else if (scrollDirection == ScrollDirection.Horizontal)
            _offsetToViewport = new Vector2(contentRect.anchoredPosition.x + _leftTopPosition.x, _leftTopPosition.y);
        return _offsetToViewport;
    }

    private bool IsInViewportAbove(Vector2 _leftTopPosition)
    {
        //此处比较的前提是视口原点在左上角，若传入点位y值大于Grid槽位高度，说明其左上角Grid从上方超出视口范围内了
        return GridSlotSize.y < CaculateGridBundleOffsetToViewport(_leftTopPosition).y;
    }

    private bool IsInViewportBelow(Vector2 _leftTopPosition)
    {
        //此处比较的前提是视口原点在左上角，viewportRect.rect.height即视口高度，视口下边界y值即-viewportRect.rect.height
        return CaculateGridBundleOffsetToViewport(_leftTopPosition).y < -viewportRect.rect.height;
    }

    private bool IsInViewportLeft(Vector2 _leftTopPosition)
    {
        //此处比较的前提是视口原点在左上角，若传入点位x值小于Grid槽位宽度的相反数，说明其左上角Grid从左侧超出视口范围内了
        return CaculateGridBundleOffsetToViewport(_leftTopPosition).x < -GridSlotSize.x;
    }

    private bool IsInViewportRight(Vector2 _leftTopPosition)
    {
        //此处比较的前提是视口原点在左上角，viewportRect.rect.width即视口宽度，视口右边界x值即+viewportRect.rect.width 
        return viewportRect.rect.width < CaculateGridBundleOffsetToViewport(_leftTopPosition).x;
    }

    private bool IsInViewport(Vector2 _leftTopPosition)
    {
        //若返回false，则说明目标Bundle所有Grid都不在视口内
        if (scrollDirection == ScrollDirection.Vertical)
            return !IsInViewportAbove(_leftTopPosition) && !IsInViewportBelow(_leftTopPosition);
        else if (scrollDirection == ScrollDirection.Horizontal)
            return !IsInViewportLeft(_leftTopPosition) && !IsInViewportRight(_leftTopPosition);
        return false;
    }
    #endregion
}