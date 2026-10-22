using System.Collections.Generic;

namespace BetterGenshinImpact.Platform.Mac;

/// <summary>
/// Windows 虚拟键码（VK_*，原版配置与按键模拟统一使用）与 macOS 虚拟键码（kVK_*）互相转换。
/// GeForce NOW 会把 Mac 键盘事件转换成云端 Windows 的按键：Option → Alt，Command → Win。
/// </summary>
public static class MacKeyMap
{
    private static readonly Dictionary<int, ushort> VkToMac = new()
    {
        // 字母
        [0x41] = 0, // A
        [0x53] = 1, // S
        [0x44] = 2, // D
        [0x46] = 3, // F
        [0x48] = 4, // H
        [0x47] = 5, // G
        [0x5A] = 6, // Z
        [0x58] = 7, // X
        [0x43] = 8, // C
        [0x56] = 9, // V
        [0x42] = 11, // B
        [0x51] = 12, // Q
        [0x57] = 13, // W
        [0x45] = 14, // E
        [0x52] = 15, // R
        [0x59] = 16, // Y
        [0x54] = 17, // T
        [0x4F] = 31, // O
        [0x55] = 32, // U
        [0x49] = 34, // I
        [0x50] = 35, // P
        [0x4C] = 37, // L
        [0x4A] = 38, // J
        [0x4B] = 40, // K
        [0x4E] = 45, // N
        [0x4D] = 46, // M

        // 数字
        [0x31] = 18,
        [0x32] = 19,
        [0x33] = 20,
        [0x34] = 21,
        [0x36] = 22,
        [0x35] = 23,
        [0x39] = 25,
        [0x37] = 26,
        [0x38] = 28,
        [0x30] = 29,

        // 符号（OEM）
        [0xBB] = 24, // = +
        [0xBD] = 27, // - _
        [0xDD] = 30, // ]
        [0xDB] = 33, // [
        [0xDE] = 39, // '
        [0xBA] = 41, // ;
        [0xDC] = 42, // \
        [0xBC] = 43, // ,
        [0xBF] = 44, // /
        [0xBE] = 47, // .
        [0xC0] = 50, // `
        [0xE2] = 10, // ISO § / OEM_102

        // 控制键
        [0x0D] = 36, // Return
        [0x09] = 48, // Tab
        [0x20] = 49, // Space
        [0x08] = 51, // Backspace
        [0x1B] = 53, // Escape
        [0x5C] = 54, // RWin → 右 Command
        [0x5B] = 55, // LWin → Command
        [0x10] = 56, // Shift
        [0xA0] = 56, // LShift
        [0x14] = 57, // CapsLock
        [0x12] = 58, // Alt → Option
        [0xA4] = 58, // LAlt
        [0x11] = 59, // Control
        [0xA2] = 59, // LControl
        [0xA1] = 60, // RShift
        [0xA5] = 61, // RAlt
        [0xA3] = 62, // RControl

        // 功能键
        [0x70] = 122, // F1
        [0x71] = 120, // F2
        [0x72] = 99, // F3
        [0x73] = 118, // F4
        [0x74] = 96, // F5
        [0x75] = 97, // F6
        [0x76] = 98, // F7
        [0x77] = 100, // F8
        [0x78] = 101, // F9
        [0x79] = 109, // F10
        [0x7A] = 103, // F11
        [0x7B] = 111, // F12
        [0x7C] = 105, // F13
        [0x7D] = 107, // F14
        [0x7E] = 113, // F15
        [0x7F] = 106, // F16
        [0x80] = 64, // F17
        [0x81] = 79, // F18
        [0x82] = 80, // F19
        [0x83] = 90, // F20

        // 方向与编辑
        [0x2D] = 114, // Insert → Help
        [0x24] = 115, // Home
        [0x21] = 116, // PageUp
        [0x2E] = 117, // Delete（向前删除）
        [0x23] = 119, // End
        [0x22] = 121, // PageDown
        [0x25] = 123, // Left
        [0x27] = 124, // Right
        [0x28] = 125, // Down
        [0x26] = 126, // Up

        // 小键盘
        [0x6E] = 65, // Decimal
        [0x6A] = 67, // Multiply
        [0x6B] = 69, // Add
        [0x90] = 71, // NumLock → Clear
        [0x6F] = 75, // Divide
        [0x6D] = 78, // Subtract
        [0x60] = 82,
        [0x61] = 83,
        [0x62] = 84,
        [0x63] = 85,
        [0x64] = 86,
        [0x65] = 87,
        [0x66] = 88,
        [0x67] = 89,
        [0x68] = 91,
        [0x69] = 92,

        // 媒体
        [0xAF] = 72, // VolumeUp
        [0xAE] = 73, // VolumeDown
        [0xAD] = 74, // VolumeMute
    };

    private static readonly Dictionary<ushort, int> MacToVk = BuildReverse();

    private static Dictionary<ushort, int> BuildReverse()
    {
        var map = new Dictionary<ushort, int>();
        foreach (var (vk, mac) in VkToMac)
        {
            // 左右区分的修饰键取左侧通用码之外的具体码，其余取第一个出现的映射
            map.TryAdd(mac, vk);
        }

        map[56] = 0xA0;
        map[58] = 0xA4;
        map[59] = 0xA2;
        map[76] = 0x0D; // 小键盘 Enter
        return map;
    }

    public static bool TryGetMacKeyCode(int virtualKey, out ushort macKeyCode) => VkToMac.TryGetValue(virtualKey, out macKeyCode);

    public static int ToVirtualKey(ushort macKeyCode) => MacToVk.TryGetValue(macKeyCode, out var vk) ? vk : 0;

    /// <summary>
    /// 该 VK 是否为修饰键，以及对应的 CGEventFlags 掩码
    /// </summary>
    public static ulong ModifierFlag(int virtualKey) => virtualKey switch
    {
        0x10 or 0xA0 or 0xA1 => MacNative.kCGEventFlagMaskShift,
        0x11 or 0xA2 or 0xA3 => MacNative.kCGEventFlagMaskControl,
        0x12 or 0xA4 or 0xA5 => MacNative.kCGEventFlagMaskAlternate,
        0x5B or 0x5C => MacNative.kCGEventFlagMaskCommand,
        _ => 0,
    };
}
