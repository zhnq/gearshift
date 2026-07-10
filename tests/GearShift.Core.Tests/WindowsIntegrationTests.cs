using GearShift.Core.System;
using Xunit;

namespace GearShift.Core.Tests;

public class WindowsIntegrationTests
{
    [Fact]
    public void Audio_endpoint_enumeration_is_available_on_windows()
    {
        var endpoints = new AudioDeviceManager().PlaybackDevices();
        Assert.NotNull(endpoints);
    }

    [Fact]
    public void Window_layout_capture_is_safe_when_no_windows_exist()
    {
        var layouts = new WindowLayoutManager().CaptureVisible();
        Assert.NotNull(layouts);
    }
}
