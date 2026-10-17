using System;
using System.Globalization;

public class CPHInline
{
    public bool Execute()
    {
        // ============================================
        // 1. READ TWITCH CHAT INPUT
        // ============================================
        // Examples:
        // !ptz set right 10
        // !ptz set leftup 5
        // !ptz set zoom 20
        string input = args.ContainsKey("rawInput") ? args["rawInput"].ToString().Trim() : "";
        string[] parts = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        // Require a command and a numerical value
        // ============================================
        // IGNORE STATUS COMMAND
        // ============================================
        // ============================================
        // COMMANDS HANDLED BY SEPARATE ACTIONS
        // ============================================
        // These commands have their own Streamer.bot
        // actions, so the main PTZ parser ignores them.
        //
        // !ptz status  - Reports camera position
        // !ptz horizon - Aligns the camera with waterline
        if (parts.Length == 1)
        {
            string command = parts[0].ToLowerInvariant();
            if (command == "status" || command == "horizon")
            {
                return false;
            }
        }

        // Require two arguments for movement or zoom.
        if (parts.Length != 2)
        {
            CPH.SendMessage("PTZ: Usage: !ptz right 5 or !ptz zoom 20");
            return false;
        }

        string direction = parts[0].ToLowerInvariant();
        string value = parts[1];
        // ============================================
        // 2. VALIDATE NUMERICAL INPUT
        // ============================================
        // ============================================
        // PARSE NUMERIC INPUT
        // ============================================
        // Accept decimal seconds using a dot, e.g. 3.5.
        // Zoom percentages must still be whole numbers.
        double number;
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || double.IsNaN(number) || double.IsInfinity(number))
        {
            CPH.SendMessage("PTZ: Please enter a valid number.");
            return false;
        }

        // Zoom uses whole-number percentages only.
        if (direction == "zoom" && number != Math.Truncate(number))
        {
            CPH.SendMessage("PTZ: Zoom must be a whole percentage.");
            return false;
        }

        // ============================================
        // 3. HANDLE PRECISE ZOOM POSITIONING
        // ============================================
        if (direction == "zoom")
        {
            // ============================================
            // ZOOM PERCENTAGE CONVERSION
            // ============================================
            // Twitch users enter a percentage from 0-100.
            // Reolink uses zoom positions from 0-41.
            //
            // Example:
            // !ptz zoom 50
            // Converts 50% to API position 21.
            if (number < 0 || number > 100)
            {
                CPH.SendMessage("PTZ: Zoom must be between 0% and 100%.");
                return false;
            }

            // Convert percentage to Reolink position.
            int zoomPosition = (int)Math.Round(number * 41.0 / 100.0, MidpointRounding.AwayFromZero);
            // Store values for the existing zoom controller.
            CPH.SetArgument("ptzDirection", "zoom");
            CPH.SetArgument("ptzZoomPosition", zoomPosition);
            CPH.SetArgument("ptzZoomPercent", (int)number);
            CPH.LogInfo($"PTZ: Zoom {number}% -> API position {zoomPosition}");
            return true;
        }

        // ============================================
        // 4. HANDLE TIMED DIRECTIONAL MOVEMENT
        // ============================================
        // Maximum chat movement: 60 seconds
        if (number < 0.1 || number > 60)
        {
            CPH.SendMessage("PTZ: Movement duration must be 1-60 seconds.");
            return false;
        }

        // ============================================
        // STORE MOVEMENT ARGUMENTS
        // ============================================
        // Tell the controller which direction to move
        CPH.SetArgument("ptzDirection", direction);
        // Store duration using a decimal point (e.g. 3.5)
        CPH.SetArgument("ptzDuration", number.ToString(CultureInfo.InvariantCulture));
        // Convert seconds to whole milliseconds
        // Example: 3.5 seconds = 3500 milliseconds
        CPH.SetArgument("ptzDelay", (int)Math.Round(number * 1000));
        return true;
    }
}