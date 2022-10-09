using System;
using System.Text.Json;

namespace quiu.tests
{
    public static class JsonDocumentExtensions
    {
        public static JsonElement? GetPropertyBySelector(this JsonDocument doc, string selector)
        {
            var parts = selector.Split (new char[] { '.' });

            var elem = doc.RootElement;

            try
            {
                foreach (var part in parts)
                    elem = elem.GetProperty (part);

                return elem;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }
    }
}

