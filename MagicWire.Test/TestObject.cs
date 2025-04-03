using System.Numerics;

namespace MagicWire.Test;

[Wire]
[WireName("testObj")]
public partial class TestObject
{
    [Wire]
    private int _age;

    [Wire]
    public bool SetAge(int age)
    {
        if (age < 0)
        {
            DoSomething();
            return false;
        }

        Age = age;
        return true;
    }

    [Wire]
    public void Own(IFrontend frontend)
    {
        frontend.Own(this);
    }

    [Wire]
    public void Disown(IFrontend frontend)
    {
        frontend.Disown(this);
    }

    [Wire]
    [ToClient]
    public partial void DoSomething();
}