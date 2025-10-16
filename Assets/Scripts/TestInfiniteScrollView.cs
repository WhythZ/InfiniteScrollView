using UnityEngine;
using System.Collections.Generic;

//InfiniteScrollView的一个使用示例
public class TestInfiniteScrollView : InfiniteScrollView<TestGrid, TestGridData>
{
    //实现抽象父类中需要的抽象方法，用于更新格子的数据和视图
    protected override void ResetGrid(TestGrid _gridInstance, TestGridData _gridData, int _gridIdx)
    {
        //此处显示格子，因为只有在视口范围内的格子才会被使用当前方法刷新
        _gridInstance.gameObject.SetActive(true);
        _gridInstance.RefreshView(_gridData.iconSprite, _gridData.descString);
    }

    #region ForTest
    //临时存放用于测试的数据
    private TestGridData[] dataForTest;

    private void Start()
    {
        //生成一堆用来测试用数据，之于实际应用中则需根据业务需求获取数据（如读取服务端、配置表、本地存档等）
        List<TestGridData> dataTemplates = new List<TestGridData>();
        dataTemplates.Add(new TestGridData() { iconSprite = Resources.Load<Sprite>("Sprites/Bow"), descString = "Bow" }); //此处从Assets/Resources/下加载资源
        dataTemplates.Add(new TestGridData() { iconSprite = Resources.Load<Sprite>("Sprites/Hammer"), descString = "Ham" });
        dataTemplates.Add(new TestGridData() { iconSprite = Resources.Load<Sprite>("Sprites/Sword"), descString = "Swd" });
        dataForTest = new TestGridData[333];
        for (int _i = 0; _i < dataForTest.Length; _i++)
        {
            dataForTest[_i] = dataTemplates[_i % dataTemplates.Count];
            dataForTest[_i].descString += (_i / dataTemplates.Count + 1).ToString();
        }

        //用这些数据生成滚动列表的Content
        Initialize(dataForTest);
    }

    protected override void Update()
    {
        base.Update();

        //用于测试
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            for (int _i = 0; _i < dataForTest.Length; _i++)
            {
                //若索引对应的Grid在视口范围内，则会刷新该列表元素
                bool _visible = RefreshGridViewIfVisible(_i);
                if (_visible)
                    Debug.Log("TestInfiniteScrollView: The grid " + _i.ToString() + " is in viewport");
            }
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            //重新构造一组数据
            List<TestGridData> _newList = new List<TestGridData>();
            TestGridData _testGridData = new TestGridData() { iconSprite = Resources.Load<Sprite>("Sprites/Bow"), descString = "Bow" };
            for (int _i = 0; _i < 100; _i++)
            {
                _testGridData.descString = "New" + (_i + 1).ToString();
                _newList.Add(_testGridData);
            }
            //刷新一下界面
            Initialize(_newList);
        }
    }
    #endregion
}