using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//实现InfiniteScrollView的数据变化处理部分逻辑
public abstract partial class InfiniteScrollView<TGrid, TGridData> : MonoBehaviour where TGrid : MonoBehaviour
{
    //若目标Grid（传入的是列表元素的绝对索引）在视口范围内，则刷新其视图显示
    public bool RefreshGridViewIfVisible(int _gridIdxAbsolute)
    {
        //排除非法索引
        if (_gridIdxAbsolute < 0 || _gridIdxAbsolute >= GridDatas.Count)
            return false;
        //视口内没有东西就不刷新
        if (visibleGridBundles.Count == 0)
            return false;

        //根据列表元素绝对索引计算出其所处Bundle的定位索引
        int _headBundleLocateIdx = visibleGridBundles.First.Value.locateIdx; //视口内首个Bundle的定位索引
        int _tailBundleLocateIdx = visibleGridBundles.Last.Value.locateIdx;  //视口内末尾Bundle的定位索引
        int _targetBundleLocateIdx = _gridIdxAbsolute / rowOrColCount;       //该格子所属Bundle的定位索引

        //若目标Bundle在视口范围内则刷新目标Grid，否则无需刷新
        if (_headBundleLocateIdx <= _targetBundleLocateIdx && _targetBundleLocateIdx <= _tailBundleLocateIdx)
        {
            //计算目标该格子在其所属Bundle中的相对索引
            int _gridIdxRelative = _gridIdxAbsolute % rowOrColCount;
            //使用LINQ查找视口范围内所有Bundle中定位索引等于目标定位索引的Bundle
            TGrid _grid = visibleGridBundles.Single((a) => a.locateIdx == _targetBundleLocateIdx).Grids[_gridIdxRelative];
            //由于所有格子的数据都存放在GridDatas中，故可直接从中获取数据（但格子对象则需从对应Bundle中获取，因为Bundle是由对象池动态管理的）
            ResetGrid(_grid, GridDatas.ElementAt(_gridIdxAbsolute), _gridIdxAbsolute);
            return true;
        }
        else
            return false;
    }

    //刷新视口内所有Grid的视图显示
    public void RefreshVisibleGridBundlesView()
    {
        //视口内没有东西就不刷新
        if (visibleGridBundles.Count() == 0)
            return;

        //遍历视口范围内的所有GridBundle并刷新Grid
        int _countReachedBundle = 0;
        foreach (var _bundle in visibleGridBundles)
        {
            bool _flagReachedFinalBundle = false;
            int _curBundleGridNum = _bundle.Grids.Length;

            #region CalculateGridsIndicies
            //结束遍历
            if (_flagReachedFinalBundle == true)
                break;
            _countReachedBundle++;

            //分别计算Bundle中第一个和最后一个Grid对应的数据索引
            int _bundleHeadIdx = _bundle.locateIdx * rowOrColCount;
            int _bundleTailIdx = _bundleHeadIdx + _curBundleGridNum - 1;

            //防止Grid索引越界
            if (_bundleTailIdx >= GridDatas.Count)
            {
                _flagReachedFinalBundle = true;
                //因为最后一个Bundle中的Grid数量不一定是满的，所以此处要需重新计算确保正确
                _bundleTailIdx = GridDatas.Count - 1;
            }
            #endregion

            #region RefreshGridsInSingleBundle
            //开始刷新Bundle中的每个Grid
            int _iRelative = 0; //相对Bundle内的索引，从0到(_curBundleGridNum - 1)
            for (int _iAbsolute = _bundleHeadIdx; _iAbsolute <= _bundleTailIdx && _iRelative < _curBundleGridNum; _iAbsolute++, _iRelative++)
                ResetGrid(_bundle.Grids[_iRelative], GridDatas.ElementAt(_iAbsolute), _iAbsolute); //刷新该Grid

            //若当前是最后一个Bundle则隐藏多余的Grid
            if (_flagReachedFinalBundle == true)
            {
                while (_iRelative < _curBundleGridNum)
                {
                    try
                    {
                        _bundle.Grids[_iRelative].gameObject.SetActive(false);
                        _iRelative++;
                    }
                    catch (System.Exception)
                    {
                        throw;
                    }
                }
            }
            #endregion
        }

        //隐藏视口外的GridBundle
        int _countRemain = visibleGridBundles.Count() - _countReachedBundle;
        while (_countRemain > 0)
        {
            _countRemain--;
            //回收GridBundle入对象池
            ReleaseGridBundle(visibleGridBundles.Last.Value);
            visibleGridBundles.RemoveLast();
        }
    }

    //增删列表元素时时需调用该函数刷新Content尺寸，起到类似ContentSizeFitter组件的作用
    public void RecalculateContentSize(bool _resetContentPos)
    {
        //根据滚动方向调整Content的尺寸，需注意由于两个锚点不同，故rect.width/height和sizeDelta.x/y的含义不同
        if (scrollDirection == ScrollDirection.Vertical)
        {
            //垂直滚动视图情况下，让Content水平方向上拉伸适应父对象Viewport的宽度，垂直方向上向父对象Viewport的上侧对齐
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);

            //宽度固定跟随父对象，故此处只用重新计算高度
            //rect.width = 父对象宽度 + sizeDelta.x
            //rect.height = sizeDelta.y
            float _newWidth = LogicalGridBundleCount * GridSlotSize.y - gridSpacing.y;
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, _newWidth);
        }
        else if (scrollDirection == ScrollDirection.Horizontal)
        {
            //水平滚动视图情况下，让Content垂直方向上拉伸适应父对象Viewport的高度，水平方向上向父对象Viewport的左侧对齐
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(0, 1);

            //高度固定跟随父对象，故此处只用重新计算宽度
            //rect.width = sizeDelta.x
            //rect.height = 父对象高度 + sizeDelta.y
            float _newWidth = LogicalGridBundleCount * GridSlotSize.x - gridSpacing.x;
            contentRect.sizeDelta = new Vector2(_newWidth, contentRect.sizeDelta.y);
        }

        //归零Content的滚动进度
        if (_resetContentPos)
            contentRect.anchoredPosition = Vector2.zero;
    }
}