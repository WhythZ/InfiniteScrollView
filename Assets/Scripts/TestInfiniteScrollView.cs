using UnityEngine;
using System.Collections.Generic;

//InfiniteScrollView的一个使用示例
public class TestInfiniteScrollView : InfiniteScrollView<TestGrid, TestGridData>
{
    public List<TestGridData> dataTemplates;

    private List<TestGridData> dataList1;
    private List<TestGridData> dataList2;
    private TestGridData[] datas;

    private void Start()
    {
        dataList1 = new List<TestGridData>();
        datas = new TestGridData[100];
        for (int i = 0; i < datas.Length; i++)
        {
            int j = i % dataTemplates.Count;
            datas[i] = dataTemplates[j];
        }
        Initlize(datas);
    }

    protected override void Update()
    {
        base.Update();
        if (Input.GetKeyDown(KeyCode.Q))
        {
            for (int i = 0; i < 11111; i++)
            {
                dataList1[i] = dataTemplates[0];
                //刷新单个元素，如果在范围内则刷新对应界面的UI元素
                ElementAtDataChange(i);
            }
        }
        if (Input.GetKey(KeyCode.G))
        {
            for (int i = 0; i < 300; i++)
            {
                dataList1.Add(dataTemplates[0]);
                ElementAtDataChange(dataList1.Count - 1);
                RecalculateContentSize(false);
            }
        }
        if (Input.GetKey(KeyCode.F))
        {
            for (int i = 0; i < 300 && dataList1.Count > 300; i++)
            {
                dataList1.RemoveAt(dataList1.Count - 1);
                ElementAtDataChange(dataList1.Count - 1);
                RecalculateContentSize(false);
            }
        }
        if (Input.GetKeyDown(KeyCode.W))
        {
            dataList2 = new List<TestGridData>();
            for (int i = 0; i < 5; i++)
            {
                dataList2.Add(dataTemplates[0]);
            }
            //刷新一下界面
            Initlize(dataList2);
            //刷新视图
            //初始化函数中包含这两个函数
            //RefrashViewRangeData();
            //RecaculateContentSize();
        }
    }

    //重置格子状态
    protected override void ResetGridData(TestGrid _gridInstance, TestGridData _data, int _dataIdx)
    {
        _gridInstance.gameObject.SetActive(true);
        _gridInstance.UpdateDisplay(_data.iconSprite, _data.nameString);
    }
}