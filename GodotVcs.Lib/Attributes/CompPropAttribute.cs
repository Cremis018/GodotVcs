namespace GodotVcs.Lib;

[AttributeUsage(AttributeTargets.Property)]
public class CompPropAttribute : Attribute
{
    public object? Value { get; set; }
}