using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//对象池中的每个对象都需要提供状态重置方法，用于回收对象时调用以防止状态污染
public interface IPoolObject
{
    void Clear();
}

//实现InfiniteScrollView的对象池部分逻辑
public abstract partial class InfiniteScrollView<TGrid, TGridData> : MonoBehaviour where TGrid : MonoBehaviour
{
    private readonly Queue<GridBundle<TGrid>> _cellBundlePool = new Queue<GridBundle<TGrid>>();
    private readonly Vector2 _cellPivot = new Vector2(0, 1);
    private readonly Vector2 _cellAnchorMin = new Vector2(0, 1);
    private readonly Vector2 _cellAnchorMax = new Vector2(0, 1);

    private void ReleaseGridBundle(GridBundle<TGrid> _gridBundle)
    {
        _gridBundle.Clear();
        _cellBundlePool.Enqueue(_gridBundle);
    }

    private GridBundle<TGrid> GetGridBundle(int _itemIdx, Vector2 _postion, Vector2 _gridSize, Vector2 _gridSpace)
    {
        GridBundle<TGrid> bundle;
        Vector2 cellOffset = default;
        if (viewDirection == InfiniteScrollDirection.Vertical)
        {
            cellOffset = new Vector2(_gridSize.x + _gridSpace.x, 0);
        }
        else if (viewDirection == InfiniteScrollDirection.Horizontal)
        {
            cellOffset = new Vector2(0, -(_gridSize.y + _gridSpace.y));
        }

        if (_cellBundlePool.Count == 0)
        {
            bundle = new GridBundle<TGrid>(rowOrColCount);
            bundle.position = _postion;
            bundle.index = _itemIdx;
            int i = _itemIdx * rowOrColCount;
            int length = _itemIdx * rowOrColCount + bundle.Grids.Length;

            for (int j = 0; j < bundle.Grids.Length && i < length; j++, i++)
            {
                bundle.Grids[j] = InstantiateCell();
                bundle.Grids[j].gameObject.SetActive(false);
                RectTransform rectTransform = bundle.Grids[j].GetComponent<RectTransform>();
                ResetRectTransform(rectTransform);
                rectTransform.anchoredPosition = _postion + j * cellOffset;

                if (i < 0 || i >= Datas.Count)
                {
                    continue;
                }
                ResetGridData(bundle.Grids[j], Datas.ElementAt(i), i);
            }
        }
        else
        {
            bundle = _cellBundlePool.Dequeue();
            bundle.position = _postion;
            bundle.index = _itemIdx;
            int i = _itemIdx * rowOrColCount;
            int celllength = _itemIdx * rowOrColCount + bundle.Grids.Length;
            int j = 0;
            for (; j < bundle.Grids.Length && i < celllength; j++, i++)
            {
                RectTransform rectTransform = bundle.Grids[j].GetComponent<RectTransform>();
                ResetRectTransform(rectTransform);
                rectTransform.anchoredPosition = _postion + j * cellOffset;
                if (i < 0 || i >= Datas.Count)
                {
                    continue;
                }
                ResetGridData(bundle.Grids[j], Datas.ElementAt(i), i);
            }
        }
        return bundle;
    }

    private void ResetRectTransform(RectTransform _rectTransform)
    {
        _rectTransform.pivot = _cellPivot;
        _rectTransform.anchorMin = _cellAnchorMin;
        _rectTransform.anchorMax = _cellAnchorMax;
    }

    //调用此方法刷新UI
    protected abstract void ResetGridData(TGrid _grid, TGridData _data, int _dataIdx);

    //生成对应的Grid
    protected virtual TGrid InstantiateCell()
    {
        return Instantiate(gridPrefab, content);
    }

    //清除所有对象池中的物体
    public void ClearPoolObject()
    {
        _cellBundlePool.Clear();
    }

    public void ClearAllUIObject()
    {
        gridBundles.Clear();
        _cellBundlePool.Clear();
    }
}