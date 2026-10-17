
using System;
using System.Net;
using System.Text.RegularExpressions;

public class CPHInline
{
    // ============================================
    // 1. CAMERA CONNECTION
    // ============================================

    private const string CAMERA_IP = "IP_HERE";
    private const string USERNAME = "USERNAME_HERE";
    private const string PASSWORD = "PASSWORD_HERE";

    // ============================================
    // 2. ZOOM CONFIGURATION
    // ============================================

    // Confirmed range for Reolink RLC-823A 16X
    private const int MIN_ZOOM = 0;
    private const int MAX_ZOOM = 41;

    // ============================================
    // 3. MAIN ZOOM CONTROLLER
    // ============================================

    public bool Execute()
    {
        // Read zoom position from the first C# step
        if (!CPH.TryGetArg(
            "ptzZoomPosition", out int zoomPosition))
        {
            CPH.LogError("PTZ Zoom: Missing zoom position.");
            return false;
        }

        // Validate target position
        if (zoomPosition < MIN_ZOOM ||
            zoomPosition > MAX_ZOOM)
        {
            CPH.SendMessage(
                "PTZ: Zoom must be between 0 and 41.");
            return false;
        }

        // ============================================
        // 4. BUILD REOLINK API COMMAND
        // ============================================

        // ZoomPos sets an absolute optical zoom position.
        string json =
            "[{\"cmd\":\"StartZoomFocus\",\"action\":0," +
            "\"param\":{\"ZoomFocus\":{\"channel\":0," +
            "\"op\":\"ZoomPos\",\"pos\":" +
            zoomPosition + "}}}]";

        string url =
            "https://" + CAMERA_IP +
            "/cgi-bin/api.cgi?user=" +
            Uri.EscapeDataString(USERNAME) +
            "&password=" +
            Uri.EscapeDataString(PASSWORD);

        try
        {
            // Camera uses a local HTTPS certificate.
            ServicePointManager.ServerCertificateValidationCallback =
                (sender, cert, chain, errors) => true;

            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] =
                    "application/json";

                // Send zoom command
                string response = client.UploadString(
                    url, "POST", json);

                // Verify camera accepted the request
                if (!Regex.IsMatch(
                    response, "\"code\"\\s*:\\s*0"))
                {
                    throw new Exception(response);
                }
            }

            CPH.LogInfo(
                $"PTZ: Zoom position {zoomPosition} requested.");

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("PTZ Zoom: " + ex.Message);
            CPH.SendMessage(
                "PTZ: Failed to set zoom position.");
            return false;
        }
    }
}
