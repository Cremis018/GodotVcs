using GodotVcs.Lib;

namespace GodotVcs.Gen.Tests;

//原来的
[Component]
public partial class MyComp : IComponent
{
    [CompProp(Value = 1)] public int Prop1 { get => GetProp1(); set => SetProp1(value); }
    [CompProp] public float Prop2 { get => GetProp2(); set => SetProp2(value); }
    // public string Prop3 { get => GetProp3(); set => SetProp3(value); } 这个就没法生成，因为不是组件属性
}