using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//对象池中的每个对象都需要提供状态重置方法，用于回收对象时调用以防止状态污染
public interface IPoolObject
{
    void HideAllGrids();
}

//实现InfiniteScrollView的对象池部分逻辑
public abstract partial class InfiniteScrollView<TGrid, TGridData> : MonoBehaviour where TGrid : MonoBehaviour
{
    //池中元素GridBundle是一行或一列TGrid
    private readonly Queue<GridBundle<TGrid>> gridBundlePool = new Queue<GridBundle<TGrid>>();
    private readonly Vector2 gridPivot = new Vector2(0, 1);
    private readonly Vector2 gridAnchorMin = new Vector2(0, 1);
    private readonly Vector2 gridAnchorMax = new Vector2(0, 1);

    //回收一个GridBundle元素
    private void ReleaseGridBundle(GridBundle<TGrid> _gridBundle)
    {
        _gridBundle.HideAllGrids();
        gridBundlePool.Enqueue(_gridBundle);
    }

    //获取一个GridBundle元素，放到对应位置
    private GridBundle<TGrid> GetGridBundle(int _itemIdx, Vector2 _postion, Vector2 _gridSize, Vector2 _gridSpace)
    {
        //临时创建一个
        GridBundle<TGrid> _bundle;

        //针对垂直或水平样式
        Vector2 _gridOffset = default;
        if (scrollDir == ScrollDirection.Vertical)
            _gridOffset = new Vector2(_gridSize.x + _gridSpace.x, 0);
        else if (scrollDir == ScrollDirection.Horizontal)
            _gridOffset = new Vector2(0, -(_gridSize.y + _gridSpace.y));

        if (gridBundlePool.Count == 0)
        {
            _bundle = new GridBundle<TGrid>(rowOrColCount);
            _bundle.position = _postion;
            _bundle.index = _itemIdx;
            int _i = _itemIdx * rowOrColCount;
            int _length = _itemIdx * rowOrColCount + _bundle.Grids.Length;

            for (int j = 0; j < _bundle.Grids.Length && _i < _length; j++, _i++)
            {
                _bundle.Grids[j] = InstantiateGrid();
                _bundle.Grids[j].gameObject.SetActive(false);
                RectTransform _rectTransform = _bundle.Grids[j].GetComponent<RectTransform>();
                ResetRectTransform(_rectTransform);
                _rectTransform.anchoredPosition = _postion + j * _gridOffset;

                if (_i < 0 || _i >= Datas.Count)
                    continue;
                ResetGridData(_bundle.Grids[j], Datas.ElementAt(_i), _i);
            }
        }
        else
        {
            _bundle = gridBundlePool.Dequeue();
            _bundle.position = _postion;
            _bundle.index = _itemIdx;
            int i = _itemIdx * rowOrColCount;
            int celllength = _itemIdx * rowOrColCount + _bundle.Grids.Length;
            int j = 0;
            for (; j < _bundle.Grids.Length && i < celllength; j++, i++)
            {
                RectTransform rectTransform = _bundle.Grids[j].GetComponent<RectTransform>();
                ResetRectTransform(rectTransform);
                rectTransform.anchoredPosition = _postion + j * _gridOffset;
                if (i < 0 || i >= Datas.Count)
                {
                    continue;
                }
                ResetGridData(_bundle.Grids[j], Datas.ElementAt(i), i);
            }
        }
        return _bundle;
    }

    private void ResetRectTransform(RectTransform _rectTransform)
    {
        _rectTransform.pivot = gridPivot;
        _rectTransform.anchorMin = gridAnchorMin;
        _rectTransform.anchorMax = gridAnchorMax;
    }

    //需子类实现的抽象方法，用于重置GridBundle中单个格子的数据状态
    protected abstract void ResetGridData(TGrid _grid, TGridData _data, int _dataIdx);

    //生成对应的TGrid
    protected virtual TGrid InstantiateGrid()
    {
        return Instantiate(gridPrefab, contentRect);
    }

    //清除所有对象池中的物体
    public void ClearPoolObject()
    {
        gridBundlePool.Clear();
    }

    public void ClearAllUIObject()
    {
        gridBundles.Clear();
        gridBundlePool.Clear();
    }
}