using UnityEngine;
using UnityEngine.UI;

//类TestGrid对应的数据结构体，实际业务中元素的数据结构通常更为复杂
[System.Serializable]
public struct TestGridData
{
    public Sprite iconSprite; //赋值给Image组件的sprite
    public string descString; //赋值给Text组件的text
}

//挂载在列表元素预制体上的脚本类
public class TestGrid : MonoBehaviour
{
    public Image iconImg;
    public Text descTxt;

    public void RefreshGridView(Sprite _sprite, string _text)
    {
        iconImg.sprite = _sprite;
        descTxt.text = _text;
    }
}