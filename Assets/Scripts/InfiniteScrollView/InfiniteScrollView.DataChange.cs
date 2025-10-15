using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//实现InfiniteScrollView的数据变化处理部分逻辑
public abstract partial class InfiniteScrollView<TGrid, TGridData> : MonoBehaviour where TGrid : MonoBehaviour
{
    //刷新界面内对应索引的Grid的显示信息
    public void ElementAtDataChange(int _idx)
    {
        if (_idx < 0 || _idx >= Datas.Count)
            throw new System.Exception("InfiniteScrollView.ElementAtDataChange接收的索引越界了");
        if (gridBundles.Count == 0)
            return;

        int _firstIndex = gridBundles.First.Value.index;
        int _lastIndex = gridBundles.Last.Value.index;
        int _targetItemIndex = _idx / rowOrColCount;
        int _targetIndex = _idx % rowOrColCount; //计算出对应的索引

        if (_targetItemIndex >= _firstIndex && _targetItemIndex <= _lastIndex)
        {
            TGrid _grid = gridBundles.Single((a) => a.index == _targetItemIndex).Grids[_targetIndex];
            ResetGridData(_grid, Datas.ElementAt(_idx), _idx);
        }
    }

    //刷新界面内所有Grid的显示信息
    public void RefrashViewRangeData()
    {
        if (gridBundles.Count() == 0)
            return;
        LinkedListNode<GridBundle<TGrid>> _curNode = gridBundles.First;
        bool _flag = false;
        int _count = 0;

        foreach (var _bundle in gridBundles)
        {
            if (_flag == true)
            {
                break;
            }
            _count++;
            int _startIdx = _bundle.index * rowOrColCount;
            int _endIdx = _startIdx + _bundle.Grids.Length - 1;

            //防止越界
            if (_endIdx >= Datas.Count)
            {
                _flag = true;
                _endIdx = Datas.Count - 1;
            }
            int i = _startIdx, j = 0;
            for (; i <= _endIdx && j < _bundle.Grids.Length; i++, j++)
            {
                ResetGridData(_bundle.Grids[j], Datas.ElementAt(i), i);
            }
            if (_flag == true)
            {
                while (j < _bundle.Grids.Length)
                {
                    try
                    {
                        _bundle.Grids[j++].gameObject.SetActive(false);
                    }
                    catch (System.Exception)
                    {
                        throw;
                    }
                }
            }
        }

        int _countRemain = gridBundles.Count() - _count;
        while (_countRemain > 0)
        {
            _countRemain--;
            ReleaseGridBundle(gridBundles.Last.Value);
            gridBundles.RemoveLast();
        }
    }

    //刷新Content的尺寸,当删除元素或者增加元素的时候请调用它
    public void RecalculateContentSize(bool _resetContentPos)
    {
        int _itemCount = ItemCount;
        if (viewDirection == InfiniteScrollDirection.Vertical)
        {
            content.anchorMin = VerticalContentAnchorMin;
            content.anchorMax = VerticalContentAnchorMax;
            content.sizeDelta = new Vector2(content.sizeDelta.x, _itemCount * slotSize.y - gridSpace.y);
        }
        else if (viewDirection == InfiniteScrollDirection.Horizontal)
        {
            content.anchorMin = HorizontalContentAnchorMin;
            content.anchorMax = HorizontalContentAnchorMax;
            content.sizeDelta = new Vector2(_itemCount * slotSize.x - gridSpace.x, content.sizeDelta.y);
        }
        if (_resetContentPos)
        {
            content.anchoredPosition = Vector2.zero;
        }
    }
}