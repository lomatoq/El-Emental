using System;
using System.Collections.Generic;

namespace Elemental.Simulation.Magic
{
    public interface IMagicCommandSink
    {
        bool Execute(in MagicCommand command);
    }

    public sealed class MagicReplayRecorder
    {
        private readonly List<MagicCommand> _commands = new List<MagicCommand>(64);

        public int Count => _commands.Count;

        public void Record(in MagicCommand command)
        {
            if (_commands.Count > 0 && command.Tick < _commands[_commands.Count - 1].Tick)
            {
                throw new InvalidOperationException("Replay commands must be recorded in tick order.");
            }

            _commands.Add(command);
        }

        public MagicCommand Get(int index)
        {
            return _commands[index];
        }
    }

    public static class MagicReplayRunner
    {
        public static int Run(MagicReplayRecorder replay, uint durationTicks, IMagicCommandSink sink)
        {
            if (replay == null)
            {
                throw new ArgumentNullException(nameof(replay));
            }

            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            int executed = 0;
            for (int index = 0; index < replay.Count; index++)
            {
                MagicCommand command = replay.Get(index);
                if (command.Tick > durationTicks)
                {
                    break;
                }

                if (sink.Execute(in command))
                {
                    executed++;
                }
            }

            return executed;
        }
    }
}
