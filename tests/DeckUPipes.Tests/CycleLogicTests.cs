using DeckUPipes.Core;

namespace DeckUPipes.Tests;

public class CycleLogicTests
{
    [Fact]
    public void NextAppIndex_Steps_Forward_And_Wraps()
    {
        // 2 apps + SYSTEM: cycle is SYSTEM(-1) -> 0 -> 1 -> SYSTEM...
        Assert.Equal(0, CycleLogic.NextAppIndex(CycleLogic.SystemAppIndex, 2, 1));
        Assert.Equal(1, CycleLogic.NextAppIndex(0, 2, 1));
        Assert.Equal(CycleLogic.SystemAppIndex, CycleLogic.NextAppIndex(1, 2, 1));
        Assert.Equal(CycleLogic.SystemAppIndex, CycleLogic.NextAppIndex(0, 2, -1));
        Assert.Equal(0, CycleLogic.NextAppIndex(1, 2, -1));
    }

    [Fact]
    public void NextAppIndex_With_Single_App_Cycles_Through_System()
    {
        Assert.Equal(0, CycleLogic.NextAppIndex(CycleLogic.SystemAppIndex, 1, 1));
        Assert.Equal(CycleLogic.SystemAppIndex, CycleLogic.NextAppIndex(0, 1, 1));
        Assert.Equal(0, CycleLogic.NextAppIndex(CycleLogic.SystemAppIndex, 1, -1));
    }

    [Fact]
    public void NextAppIndex_With_No_Apps_Stays_On_System()
    {
        Assert.Equal(CycleLogic.SystemAppIndex, CycleLogic.NextAppIndex(CycleLogic.SystemAppIndex, 0, 1));
        Assert.Equal(CycleLogic.SystemAppIndex, CycleLogic.NextAppIndex(CycleLogic.SystemAppIndex, 0, -1));
    }

    [Fact]
    public void NextTabTarget_Is_Null_When_System_Is_Next()
    {
        Assert.Null(CycleLogic.NextTabTargetAppIndex(1, 2)); // after last app -> SYSTEM
        Assert.Null(CycleLogic.NextTabTargetAppIndex(0, 0));
    }

    [Fact]
    public void NextTabTarget_Points_At_The_Following_App()
    {
        Assert.Equal(1, CycleLogic.NextTabTargetAppIndex(0, 2));
        Assert.Equal(0, CycleLogic.NextTabTargetAppIndex(CycleLogic.SystemAppIndex, 2));
    }
}

