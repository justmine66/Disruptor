using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Disruptor.Utility
{
    /// <summary>
    /// Provides static methods for managing a <see cref="ISequenceGroup"/> object.
    /// </summary>
    internal static class SequenceGroupManager
    {
        internal static long GetMinimumSequence(ISequence[] sequences, long minimum = long.MaxValue)
        {
            return sequences.Select(sequence => sequence.GetValue()).Concat(new[] {minimum}).Min();
        }

        internal static void AddSequences(ref ISequence[] sequences, ICursored cursor, params ISequence[] sequencesToAdd)
        {
            long cursorSequence;
            ISequence[] updatedSequences;
            ISequence[] currentSequences;

            do
            {
                currentSequences = Volatile.Read(ref sequences);
                var currentLength = currentSequences.Length;
                updatedSequences = new ISequence[currentLength + sequencesToAdd.Length];

                Array.Copy(currentSequences, updatedSequences, currentLength);
                cursorSequence = cursor.GetCursor();

                foreach (var sequence in sequencesToAdd)
                {
                    sequence.SetValue(cursorSequence);
                    updatedSequences[currentLength++] = sequence;
                }
            } while (Interlocked.CompareExchange(ref sequences, updatedSequences, currentSequences) != currentSequences);

            cursorSequence = cursor.GetCursor();
            foreach (var sequence in sequencesToAdd)
            {
                sequence.SetValue(cursorSequence);
            }
        }

        internal static bool RemoveSequence(ref ISequence[] sequences, ISequence sequenceToRemove)
        {
            int numToRemove;
            ISequence[] oldSequences;
            ISequence[] newSequences;

            do
            {
                oldSequences = Volatile.Read(ref sequences);
                numToRemove = CountMatching(oldSequences, sequenceToRemove);

                if (0 == numToRemove)
                {
                    break;
                }

                var oldSize = oldSequences.Length;
                newSequences = new ISequence[oldSize - numToRemove];

                for (int i = 0, pos = 0; i < oldSize; i++)
                {
                    var sequence = oldSequences[i];
                    if (sequence != sequenceToRemove)
                    {
                        newSequences[pos++] = sequence;
                    }
                }
            } while (Interlocked.CompareExchange(ref sequences, newSequences, oldSequences) != oldSequences);

            return numToRemove != 0;
        }

        /// <summary>
        /// Get an array of <see cref="ISequence"/>s for the passed <see cref="IEventProcessor"/>s
        /// </summary>
        /// <param name="processors">for which to get the sequences</param>
        /// <returns></returns>
        internal static IEnumerable<ISequence> GetSequencesFor(params IEventProcessor[] processors)
        {
            var sequences = new ISequence[processors.Length];
            for (var i = 0; i < sequences.Length; i++)
            {
                sequences[i] = processors[i].GetSequence();
            }

            return sequences;
        }

        private static int CountMatching(ISequence[] sequences, ISequence toMatch)
        {
            return sequences.Count(sequence => sequence == toMatch);
        }
    }
}
