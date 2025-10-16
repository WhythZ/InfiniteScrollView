using UnityEngine;
using System.Collections.Generic;

//InfiniteScrollView的一个使用示例
public class TestInfiniteScrollView : InfiniteScrollView<TestGrid, TestGridData>
{
    public List<TestGridData> dataTemplates;
    private TestGridData[] dataForTest;

    private void Start()
    {
        //根据数据模板生成一堆用来测试用数据，之于实际应用中则需根据业务需求获取数据
        dataForTest = new TestGridData[333];
        for (int i = 0; i < dataForTest.Length; i++)
        {
            dataForTest[i] = dataTemplates[i % dataTemplates.Count];
            dataForTest[i].descString += (i / dataTemplates.Count + 1).ToString();
        }

        //用这些数据生成滚动列表的Content
        Initialize(dataForTest);
    }

    protected override void Update()
    {
        base.Update();
        //if (Input.GetKeyDown(KeyCode.Q))
        //{
        //    for (int i = 0; i < 11111; i++)
        //    {
        //        dataList1[i] = dataTemplates[0];
        //        //刷新单个元素，如果在范围内则刷新对应界面的UI元素
        //        ElementAtDataChange(i);
        //    }
        //}
        //if (Input.GetKey(KeyCode.G))
        //{
        //    for (int i = 0; i < 300; i++)
        //    {
        //        dataList1.Add(dataTemplates[0]);
        //        ElementAtDataChange(dataList1.Count - 1);
        //        RecalculateContentSize(false);
        //    }
        //}
        //if (Input.GetKey(KeyCode.F))
        //{
        //    for (int i = 0; i < 300 && dataList1.Count > 300; i++)
        //    {
        //        dataList1.RemoveAt(dataList1.Count - 1);
        //        ElementAtDataChange(dataList1.Count - 1);
        //        RecalculateContentSize(false);
        //    }
        //}
        //if (Input.GetKeyDown(KeyCode.W))
        //{
        //    dataList2 = new List<TestGridData>();
        //    for (int i = 0; i < 5; i++)
        //    {
        //        dataList2.Add(dataTemplates[0]);
        //    }
        //    //刷新一下界面
        //    Initialize(dataList2);
        //    //刷新视图
        //    //初始化函数中包含这两个函数
        //    //RefrashViewRangeData();
        //    //RecaculateContentSize();
        //}
    }

    //实现抽象父类中需要的抽象方法，用于重置格子状态
    protected override void ResetGridData(TestGrid _gridInstance, TestGridData _data, int _dataIdx)
    {
        _gridInstance.gameObject.SetActive(true);
        _gridInstance.RefreshGridView(_data.iconSprite, _data.descString);
    }
}