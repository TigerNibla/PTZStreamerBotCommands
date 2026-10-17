
using System;
using System.Net;
using System.Text.RegularExpressions;

public class CPHInline
{
    // ============================================
    // 1. CAMERA CONNECTION
    // ============================================

    private const string CAMERA_IP = "IP_GOES_HERE";
    private const string USERNAME = "USERNAME_HERE";
    private const string PASSWORD = "PASSWORD_HERE";

    // ============================================
    // 2. PAN CONFIGURATION
    // ============================================

    // User-defined horizontal boundaries
    // Larger Ppos means further LEFT.
    private const int LEFT_POSITION = 1386;
    private const int RIGHT_POSITION = 1087;

    // Center of the allowed horizontal range
    private const double CENTER_POSITION =
        (LEFT_POSITION + RIGHT_POSITION) / 2.0;

    // Within this distance, report "Center"
    private const int CENTER_TOLERANCE = 5;

    // ============================================
    // 3. ZOOM CONFIGURATION
    // ============================================

    // Confirmed RLC-823A 16X zoom range
    private const int MIN_ZOOM = 0;
    private const int MAX_ZOOM = 41;

    // ============================================
    // 4. CAMERA API COMMUNICATION
    // ============================================

    private string ApiRequest(string json)
    {
        string url =
            "https://" + CAMERA_IP +
            "/cgi-bin/api.cgi?user=" +
            Uri.EscapeDataString(USERNAME) +
            "&password=" +
            Uri.EscapeDataString(PASSWORD);

        using (var client = new WebClient())
        {
            client.Headers[HttpRequestHeader.ContentType] =
                "application/json";

            return client.UploadString(url, "POST", json);
        }
    }

    // ============================================
    // 5. READ PAN POSITION
    // ============================================

    private int GetPanPosition()
    {
        string json =
            "[{\"cmd\":\"GetPtzCurPos\",\"action\":1," +
            "\"param\":{\"PtzCurPos\":{\"channel\":0}}}]";

        string response = ApiRequest(json);

        if (!Regex.IsMatch(
            response, "\"code\"\\s*:\\s*0"))
            throw new Exception("Failed to read pan position");

        Match match = Regex.Match(
            response, "\"Ppos\"\\s*:\\s*(\\d+)");

        if (!match.Success)
            throw new Exception("Pan position missing");

        return int.Parse(match.Groups[1].Value);
    }

    // ============================================
    // 6. READ ZOOM POSITION
    // ============================================

    private int GetZoomPosition()
    {
        string json =
            "[{\"cmd\":\"GetZoomFocus\",\"action\":1," +
            "\"param\":{\"channel\":0}}]";

        string response = ApiRequest(json);

        if (!Regex.IsMatch(
            response, "\"code\"\\s*:\\s*0"))
            throw new Exception("Failed to read zoom position");

        // Match the zoom position in the "value" section,
        // rather than the "initial" or "range" sections.
        int valueIndex = response.IndexOf("\"value\"");

        if (valueIndex < 0)
            throw new Exception("Zoom value missing");

        string valueSection = response.Substring(valueIndex);

        Match match = Regex.Match(
            valueSection,
            "\"zoom\"\\s*:\\s*\\{\\s*\"pos\"\\s*:\\s*(\\d+)");

        if (!match.Success)
            throw new Exception("Zoom position missing");

        return int.Parse(match.Groups[1].Value);
    }

    // ============================================
    // 7. CONVERT PAN POSITION TO HUMAN TEXT
    // ============================================

    private string FormatPanPosition(int position)
    {
        double difference = position - CENTER_POSITION;

        // Camera is approximately centered
        if (Math.Abs(difference) <= CENTER_TOLERANCE)
            return "Center";

        // Left side of the allowed range
        if (difference > 0)
        {
            double percent =
                difference / (LEFT_POSITION - CENTER_POSITION)
                * 100.0;

            percent = Math.Max(0, Math.Min(100, percent));

            return "Left " +
                Math.Round(percent).ToString() + "%";
        }

        // Right side of the allowed range
        double rightPercent =
            Math.Abs(difference) /
            (CENTER_POSITION - RIGHT_POSITION) * 100.0;

        rightPercent = Math.Max(0,
            Math.Min(100, rightPercent));

        return "Right " +
            Math.Round(rightPercent).ToString() + "%";
    }

    // ============================================
    // 8. MAIN STREAMER.BOT ACTION
    // ============================================

    public bool Execute()
    {
        try
        {
            // Allow the camera's local HTTPS certificate
            ServicePointManager.ServerCertificateValidationCallback =
                (sender, cert, chain, errors) => true;

            // Read actual positions from camera
            int panPosition = GetPanPosition();
            int zoomPosition = GetZoomPosition();

            // Format horizontal position
            string panText = FormatPanPosition(panPosition);

            // Convert zoom position to 0-100%
            int zoomPercent = (int)Math.Round(
                (zoomPosition - MIN_ZOOM) * 100.0 /
                (MAX_ZOOM - MIN_ZOOM),
                MidpointRounding.AwayFromZero);

            zoomPercent = Math.Max(0,
                Math.Min(100, zoomPercent));

            // ====================================
            // SEND STATUS TO TWITCH CHAT
            // ====================================

            string message =
                "Status: " + panText +
                ". Zoom: " + zoomPercent + "%.";

            CPH.SendMessage(message);

            CPH.LogInfo("PTZ Status: " + message);

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("PTZ Status: " + ex.Message);

            CPH.SendMessage(
                "PTZ: Unable to retrieve camera status.");

            return false;
        }
    }
}
