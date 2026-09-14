using DeckUPipes.Core;

namespace DeckUPipes.Tests;

public class AudioSessionTests
{
    [Theory]
    [InlineData("", false, 100u, "spotify", "spotify")]
    [InlineData("@%SystemRoot%\\System32\\AudioSrv.Dll,-202", false, 0u, "AudioSrv", "System Sounds")]
    [InlineData("", true, 0u, "AudioSrv", "System Sounds")]
    [InlineData("Music", false, 100u, "spotify", "Music")]
    [InlineData("", false, 0u, "pid 0", "pid 0")]
    public void ResolveDisplayName_Maps_Names(string raw, bool isSystemSounds, uint processId, string processName, string expected)
    {
        Assert.Equal(expected, AudioSession.ResolveDisplayName(raw, processName, isSystemSounds, processId));
    }
}

