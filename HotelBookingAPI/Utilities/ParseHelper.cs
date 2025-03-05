using System;
using System.Text.Json;

namespace HotelBookingAPI.Utilities
{
    public static class ParseHelper
    {
        public static decimal ParseDecimalFromJson(JsonElement element, string path)
        {
            try
            {
                var targetElement = element;
                foreach (var part in path.Split('.'))
                {
                    if (part.Contains('['))
                    {
                        var arrayPart = part.Split('[');
                        var index = int.Parse(arrayPart[1].TrimEnd(']'));
                        targetElement = targetElement.GetProperty(arrayPart[0])[index];
                    }
                    else
                    {
                        targetElement = targetElement.GetProperty(part);
                    }
                }

                if (targetElement.ValueKind == JsonValueKind.String)
                {
                    return decimal.Parse(targetElement.GetString());
                }
                else if (targetElement.ValueKind == JsonValueKind.Number)
                {
                    return targetElement.GetDecimal();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse decimal from JSON path '{path}': {ex.Message}");
            }

            return -1.0m; 
        }

        public static int ParseIntFromJson(JsonElement element, string path)
        {
            try
            {
                var targetElement = element;
                foreach (var part in path.Split('.'))
                {
                    targetElement = targetElement.GetProperty(part);
                }

                if (targetElement.ValueKind == JsonValueKind.String)
                {
                    return int.Parse(targetElement.GetString());
                }
                else if (targetElement.ValueKind == JsonValueKind.Number)
                {
                    return targetElement.GetInt32();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse integer from JSON path '{path}': {ex.Message}");
            }

            return -1; 
        }
    }
}