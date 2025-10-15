using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//滚动列表样式，垂直或水平
public enum InfiniteScrollDirection
{
    Vertical,
    Horizontal
}

//用来表示多行或多列的数据
public class GridBundle<TGrid> : IPoolObject where TGrid : MonoBehaviour
{
    public int index;
    public Vector2 position;
    public TGrid[] Grids { get; private set; }
    public int CellCapacity => Grids.Length;
    public GridBundle(int gameObjectCapacity)
    {
        Grids = new TGrid[gameObjectCapacity];
    }
    public void Clear()
    {
        index = -1;
        foreach (var _grid in Grids)
        {
            if (_grid != null)
            {
                _grid.gameObject.SetActive(false);
            }
        }
    }
}

//通过继承并实现此抽象类来使用滚动列表功能（修饰partial表示该类的定义可以被拆分到多个文件中）
public abstract partial class InfiniteScrollView<TGrid, TGridData> : MonoBehaviour where TGrid : MonoBehaviour
{
    public InfiniteScrollDirection viewDirection;

    public ICollection<TGridData> Datas { get; private set; }

    [SerializeField] protected TGrid gridPrefab;
    
    private RectTransform parentRect;
    [SerializeField] protected RectTransform content;

    [SerializeField] private Vector2 gridSpace; //设置Grid间的水平或垂直方向上的间距
    [SerializeField] private int rowOrColCount; //垂直则表每行数量，水平则为每列数量

    public Vector2 contentPos => content.position;
    public Vector2 contentSize => content.sizeDelta;
    public Vector2 gridSize => gridRectTransform.sizeDelta;
    public Vector2 slotSize => gridSize + gridSpace;
    
    private RectTransform gridRectTransform;
    private readonly Vector2 HorizontalContentAnchorMin = new Vector2(0, 0);
    private readonly Vector2 HorizontalContentAnchorMax = new Vector2(0, 1);
    private readonly Vector2 VerticalContentAnchorMin = new Vector2(0, 1);
    private readonly Vector2 VerticalContentAnchorMax = new Vector2(1, 1);
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

    //Scroll的初始化和数据的长度发生变化都需要使用这个函数,这个函数只涉及到Content的大小的变化
    public virtual void Initlize(ICollection<TGridData> _datas, bool _resetPos = false)
    {
        if (_datas == null)
            throw new System.Exception("InfiniteScrollView.Initlize接收的数据为空");

        gridRectTransform = gridPrefab.GetComponent<RectTransform>();
        Datas = _datas;
        RecalculateContentSize(_resetPos);

        //清除头部和尾部
        UpdateDisplay();
        RefrashViewRangeData();
    }

    //刷新一下，数据的长度发生变化的时候使用这个函数
    public void Refrash(bool _resetContentPos = false)
    {
        RecalculateContentSize(_resetContentPos);
        content.anchoredPosition = _resetContentPos ? Vector2.zero : content.anchoredPosition;
        UpdateDisplay();
        RefrashViewRangeData();
    }

    protected virtual void Update()
    {
        if (Datas == null)
        {
            return;
        }
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
        Vector2 cellSpace = gridSpace;

        if (viewDirection == InfiniteScrollDirection.Vertical)
        {
            Vector2 topPos = -content.anchoredPosition;
            Vector2 bottomPos = new Vector2(topPos.x, topPos.y - viewRangeSize.y);
            int startIndex = GetIndex(topPos);
            int endIndex = GetIndex(bottomPos);
            for (int i = startIndex; i <= endIndex && i < itemCount; i++)
            {
                Vector2 pos = new Vector2(content.anchoredPosition.x, -i * itemSize.y);
                var bundle = GetGridBundle(i, pos, cellSize, cellSpace);
                gridBundles.AddLast(bundle);
            }
        }
        else if (viewDirection == InfiniteScrollDirection.Horizontal)
        {
            Vector2 leftPos = -content.anchoredPosition;
            Vector2 rightPos = new Vector2(leftPos.x + viewRangeSize.x, leftPos.y);

            int startIndex = GetIndex(leftPos);
            int endIndex = GetIndex(rightPos);

            for (int i = startIndex; i <= endIndex && i < itemCount; i++)
            {
                Vector2 pos = new Vector2(i * itemSize.x, content.anchoredPosition.y);
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
        if (viewDirection == InfiniteScrollDirection.Vertical)
            offset = new Vector2(0, slotSize.y);
        else if (viewDirection == InfiniteScrollDirection.Horizontal)
            offset = new Vector2(-slotSize.x, 0);

        Vector2 newHeadBundlePos = bundle.position + offset;

        while (OnViewRange(newHeadBundlePos))
        {
            int caculatedIndex = GetIndex(newHeadBundlePos);
            int index = bundle.index - 1;

            if (index < 0) break;
            if (caculatedIndex != index)
                Debug.LogError($"计算索引:{caculatedIndex},计数索引{index}计算出的索引和计数的索引值不相等...");

            bundle = GetGridBundle(index, newHeadBundlePos, gridSize, gridSpace);
            gridBundles.AddFirst(bundle);

            newHeadBundlePos = bundle.position + offset;
        }
    }

    private void RemoveHead()
    {
        if (gridBundles.Count == 0)
            return;

        if (viewDirection == InfiniteScrollDirection.Vertical)
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
        else if (viewDirection == InfiniteScrollDirection.Horizontal)
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
        if (viewDirection == InfiniteScrollDirection.Vertical)
            offset = new Vector2(0, -slotSize.y);
        else if (viewDirection == InfiniteScrollDirection.Horizontal)
            offset = new Vector2(slotSize.x, 0);

        Vector2 newTailBundlePos = bundle.position + offset;

        while (OnViewRange(newTailBundlePos))
        {
            int caculatedIndex = GetIndex(newTailBundlePos);
            int index = bundle.index + 1;

            if (index >= ItemCount) break;
            if (caculatedIndex != index)
                Debug.LogError($"计算索引:{caculatedIndex},计数索引{index}计算出的索引和计数的索引值不相等...");

            bundle = GetGridBundle(index, newTailBundlePos, gridSize, gridSpace);
            gridBundles.AddLast(bundle);

            newTailBundlePos = bundle.position + offset;
        }
    }

    private void RemoveTail()
    {
        if (gridBundles.Count == 0)
            return;

        if (viewDirection == InfiniteScrollDirection.Vertical)
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
        else if (viewDirection == InfiniteScrollDirection.Horizontal)
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
        if (viewDirection == InfiniteScrollDirection.Horizontal)
        {
            relativePosition = new Vector2(curPosition.x + content.anchoredPosition.x, curPosition.y);
        }
        else if (viewDirection == InfiniteScrollDirection.Vertical)
        {
            relativePosition = new Vector2(curPosition.x, curPosition.y + content.anchoredPosition.y);
        }
        return relativePosition;
    }

    public int GetIndex(Vector2 position)
    {
        int index = -1;
        if (viewDirection == InfiniteScrollDirection.Vertical)
        {
            index = Mathf.RoundToInt(-position.y / slotSize.y);
            return index;
        }
        else if (viewDirection == InfiniteScrollDirection.Horizontal)
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
        if (viewDirection == InfiniteScrollDirection.Horizontal)
        {
            return !InViewRangeLeft(position) && !InViewRangeRight(position);
        }
        else if (viewDirection == InfiniteScrollDirection.Vertical)
        {
            return !AboveViewRange(position) && !UnderViewRange(position);
        }
        return false;
    }
}