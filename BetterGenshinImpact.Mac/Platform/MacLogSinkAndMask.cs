using System;
using System.Collections.Generic;
using System.IO;
using BetterGenshinImpact.Core.Mask;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace BetterGenshinImpact.Platform
{
    /// <summary>
    /// 把日志转发给界面。保留最近的若干条，界面打开时可以补显示
    /// </summary>
    public sealed class MacLogSink : ILogEventSink
    {
        private const int Capacity = 2000;
        private readonly MessageTemplateTextFormatter _formatter = new("[{Timestamp:HH:mm:ss}] {Message:lj}{NewLine}{Exception}");
        private readonly LinkedList<(LogEventLevel Level, string Text)> _recent = new();

        public event Action<LogEventLevel, string>? Emitted;

        public IReadOnlyList<(LogEventLevel Level, string Text)> Snapshot()
        {
            lock (_recent)
            {
                return [.. _recent];
            }
        }

        public void Emit(LogEvent logEvent)
        {
            using var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            var text = writer.ToString().TrimEnd();
            lock (_recent)
            {
                _recent.AddLast((logEvent.Level, text));
                while (_recent.Count > Capacity)
                {
                    _recent.RemoveFirst();
                }
            }

            Emitted?.Invoke(logEvent.Level, text);
        }
    }

    /// <summary>
    /// 遮罩窗口宿主。macOS 版暂不在 GeForce NOW 窗口上叠加透明遮罩，只记录状态
    /// </summary>
    public sealed class MacMaskWindowHost : IMaskWindowHost
    {
        public MaskWindowState State { get; private set; }

        public event EventHandler<MaskWindowState>? StateChanged;

        public void Attach(nint gameHandle)
        {
        }

        public void Detach()
        {
            State = default;
            StateChanged?.Invoke(this, State);
        }

        public void Close() => Detach();

        public void ReportGameWindow(GameWindowState state)
        {
        }
    }
}
