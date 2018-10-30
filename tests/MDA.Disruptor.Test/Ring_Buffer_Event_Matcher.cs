using System.Data;

namespace MDA.Disruptor.Test
{
    public class Ring_Buffer_Event_Matcher
    {
        private readonly object[] _values;

        public Ring_Buffer_Event_Matcher(params object[] values)
        {
            _values = values;
        }
    }
}
