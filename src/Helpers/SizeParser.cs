namespace VideoOptimizer.Helpers;

public static class SizeParser
{
    public static bool TryParse(string input, out long bytes)
    {
        bytes = 0;
        if (String.IsNullOrWhiteSpace(input))
            return false;

        input = input.Trim().ToLowerInvariant();

        // Find where the unit starts
        int unitStart = 0;
        while (unitStart < input.Length
            && (Char.IsDigit(input[unitStart]) || input[unitStart] == '.' || input[unitStart] == ','))
        {
            unitStart++;
        }

        if (unitStart == 0)
            return false;

        string numberPart = input[..unitStart].Replace(',', '.');
        string unitPart = input[unitStart..].Trim();

        if (!Double.TryParse(
            numberPart,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out double number))
        {
            return false;
        }

        double multiplier;
        if (String.IsNullOrEmpty(unitPart))
        {
            // Default to Megabytes
            multiplier = 1024.0 * 1024.0;
        }
        else
        {
            switch (unitPart)
            {
                case "b":
                    multiplier = 1.0;
                    break;
                case "kb":
                case "k":
                    multiplier = 1024.0;
                    break;
                case "mb":
                case "m":
                    multiplier = 1024.0 * 1024.0;
                    break;
                case "gb":
                case "g":
                    multiplier = 1024.0 * 1024.0 * 1024.0;
                    break;
                default:
                    return false; // Unknown unit
            }
        }

        bytes = (long) (number * multiplier);
        return bytes > 0;
    }
}
