using UnityEngine;
using System.Collections.Generic;
using System.Linq;

//对象池中的每个对象都需要提供状态重置方法，用于回收对象时调用以防止状态污染
public interface IPoolObject
{
    void ResetPoolObject();
}

//实现InfiniteScrollView的对象池部分逻辑
public abstract partial class InfiniteScrollView<TGrid, TGridData> : MonoBehaviour where TGrid : MonoBehaviour
{
    //池中元素GridBundle是一行或一列TGrid（修饰readonly使得该容器无法被赋值为其它新容器或null，但其内容可以被改变，例如调用Enqueue/Dequeue/Clear等方法）
    private readonly Queue<GridBundle<TGrid>> gridBundlePool = new Queue<GridBundle<TGrid>>();

    //需子类实现（因为这取决于TGrid数据结构，这又取决于业务需求）的抽象方法，用于更新单个格子的数据和视图
    protected abstract void ResetGrid(TGrid _gridInstance, TGridData _gridData, int _gridIdx);

    //回收一个GridBundle元素
    private void ReleaseGridBundle(GridBundle<TGrid> _gridBundle)
    {
        //清空Bundle上的数据残留
        _gridBundle.ResetPoolObject();
        //回收到队列池中等待唤醒
        gridBundlePool.Enqueue(_gridBundle);
    }

    //获取一个GridBundle元素并放到对应位置，传入的是Bundle整体的左上角位置（因为每个Grid锚点和轴心都在左上角）
    private GridBundle<TGrid> GetGridBundle(int _locateIdx, Vector2 _leftTopPostion, Vector2 _gridSize, Vector2 _gridSpacing)
    {
        //用来引用队列池中Bundle
        GridBundle<TGrid> _bundle;

        //该值用于针对垂直或水平样式，计算格子所需水平或垂直方向偏移
        Vector2 _gridOffsetInBundle = default;
        if (scrollDirection == ScrollDirection.Vertical)
            _gridOffsetInBundle = new Vector2(_gridSize.x + _gridSpacing.x, 0);
        else if (scrollDirection == ScrollDirection.Horizontal)
            _gridOffsetInBundle = new Vector2(0, -(_gridSize.y + _gridSpacing.y));

        //若此时池中无对象可用则创建新对象以扩容，否则从队列池中取出复用
        if (gridBundlePool.Count == 0)
        {
            //新建一个
            _bundle = new GridBundle<TGrid>(rowOrColCount);
            _bundle.leftTopPosition = _leftTopPostion;
            _bundle.locateIdx = _locateIdx;

            //遍历该Bundle内所有Grid
            int _iAbsoluteTail = _locateIdx * rowOrColCount + _bundle.Grids.Length;
            for (int _iAbsolute = _locateIdx * rowOrColCount, _iRelative = 0; (_iAbsolute < _iAbsoluteTail) && (_iRelative < _bundle.Grids.Length); _iAbsolute++, _iRelative++)
            {
                //实例化一个Grid到Content内，并先隐藏
                _bundle.Grids[_iRelative] = Instantiate(gridPrefab, contentRect);
                _bundle.Grids[_iRelative].gameObject.SetActive(false);

                //初始化该Grid的锚框、轴心
                RectTransform _rectTransform = _bundle.Grids[_iRelative].GetComponent<RectTransform>();
                InitGridRectTransform(_rectTransform);
                //计算该Grid在当前Bundle内相对Content的锚定位置，垂直滚动则Bundle为行而位置水平偏移，水平滚动则Bundle为列而位置垂直偏移
                _rectTransform.anchoredPosition = _leftTopPostion + _iRelative * _gridOffsetInBundle;

                //排除非法索引，合法则赋予数据并刷新视图
                if (_iAbsolute < 0 || _iAbsolute >= GridDatas.Count)
                    continue;
                ResetGrid(_bundle.Grids[_iRelative], GridDatas.ElementAt(_iAbsolute), _iAbsolute);
            }
        }
        else
        {
            //取出一个
            _bundle = gridBundlePool.Dequeue();
            _bundle.leftTopPosition = _leftTopPostion;
            _bundle.locateIdx = _locateIdx;

            //遍历该Bundle内所有Grid
            int _iAbsoluteTail = _locateIdx * rowOrColCount + _bundle.Grids.Length;
            for (int _iAbsolute = _locateIdx * rowOrColCount, _iRelative = 0; (_iAbsolute < _iAbsoluteTail) && (_iRelative < _bundle.Grids.Length); _iAbsolute++, _iRelative++)
            {
                //初始化该Grid的锚框、轴心
                RectTransform _rectTransform = _bundle.Grids[_iRelative].GetComponent<RectTransform>();
                InitGridRectTransform(_rectTransform);
                //计算该Grid在当前Bundle内相对Content的锚定位置，垂直滚动则Bundle为行而位置水平偏移，水平滚动则Bundle为列而位置垂直偏移
                _rectTransform.anchoredPosition = _leftTopPostion + _iRelative * _gridOffsetInBundle;

                //排除非法索引，合法则赋予数据并刷新视图
                if (_iAbsolute < 0 || _iAbsolute >= GridDatas.Count)
                    continue;
                ResetGrid(_bundle.Grids[_iRelative], GridDatas.ElementAt(_iAbsolute), _iAbsolute);
            }
        }

        //返回这个Bundle
        return _bundle;
    }

    //将Grid的轴心与锚点均设置在左上角以便计算
    private void InitGridRectTransform(RectTransform _rectTransform)
    {
        //两锚点位置重合于左上角（重合时sizeDelta等于Grid的真实尺寸），同时让轴心也位于左上角，以便计算
        _rectTransform.pivot = new Vector2(0, 1);
        _rectTransform.anchorMin = new Vector2(0, 1);
        _rectTransform.anchorMax = new Vector2(0, 1);
    }

    //清除池中所有实例
    public void ClearAllPoolObjects()
    {
        //当前可见的
        visibleGridBundles.Clear();
        //暂不可见的
        gridBundlePool.Clear();
    }
}