using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Diagnostics;
using System.Globalization;

public class CPHInline
{
    // ============================================
    // 1. CAMERA CONNECTION
    // ============================================
    private const string CAMERA_IP = "IP_GOES_HERE";
    private const string USERNAME = "USERNAME_HERE";
    private const string PASSWORD = "PASSOWRD_HERE";
    // ============================================
    // 2. MOVEMENT CONFIGURATION
    // ============================================
    private const int SPEED = 1;
    // Maximum movement duration in seconds
    private const int MAX_DURATION = 30;
    // How often to check position, in milliseconds
    private const int CHECK_INTERVAL = 250;
    // ============================================
    // 3. HORIZONTAL MOVEMENT BOUNDARIES
    // ============================================
    // Larger Ppos values mean further LEFT
    private const int LEFT_LIMIT = 1376;
    // Smaller Ppos values mean further RIGHT
    private const int RIGHT_LIMIT = 1097;
    // ============================================
    // 4. CAMERA COMMUNICATION
    // ============================================
    private string ApiRequest(string json)
    {
        string url = "https://" + CAMERA_IP + "/cgi-bin/api.cgi?user=" + USERNAME + "&password=" + PASSWORD;
        using (var client = new CameraWebClient())
        {
            client.Headers[HttpRequestHeader.ContentType] = "application/json";
            string response = client.UploadString(url, "POST", json);
            // Reject unsuccessful API responses
            if (!Regex.IsMatch(response, "\"code\"\\s*:\\s*0"))
            {
                throw new Exception("Camera API error: " + response);
            }

            return response;
        }
    }

    // Set a timeout to avoid waiting indefinitely
    private class CameraWebClient : WebClient
    {
        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            request.Timeout = 2000;
            return request;
        }
    }

    // ============================================
    // 5. GET CURRENT PAN POSITION
    // ============================================
    private int GetPanPosition()
    {
        string json = "[{\"cmd\":\"GetPtzCurPos\"," + "\"action\":1,\"param\":" + "{\"PtzCurPos\":{\"channel\":0}}}]";
        string response = ApiRequest(json);
        Match match = Regex.Match(response, "\"Ppos\"\\s*:\\s*(\\d+)");
        if (!match.Success)
            throw new Exception("Camera did not return Ppos");
        return int.Parse(match.Groups[1].Value);
    }

    // ============================================
    // 6. SEND MOVEMENT COMMAND
    // ============================================
    private void MoveCamera(string direction)
    {
        string json = "[{\"cmd\":\"PtzCtrl\",\"action\":0," + "\"param\":{\"channel\":0," + "\"op\":\"" + direction + "\"," + "\"speed\":" + SPEED + "}}]";
        ApiRequest(json);
    }

    // ============================================
    // 7. STOP CAMERA
    // ============================================
    private void StopCamera()
    {
        string json = "[{\"cmd\":\"PtzCtrl\",\"action\":0," + "\"param\":{\"channel\":0," + "\"op\":\"Stop\"}}]";
        ApiRequest(json);
    }

    // ============================================
    // 8. CHECK HORIZONTAL BOUNDARIES
    // ============================================
    private bool AtBoundary(string direction, int position)
    {
        if (direction.Contains("left") && position >= LEFT_LIMIT)
        {
            return true;
        }

        if (direction.Contains("right") && position <= RIGHT_LIMIT)
        {
            return true;
        }

        return false;
    }

    // ============================================
    // 9. MAIN STREAMER.BOT CONTROLLER
    // ============================================
    public bool Execute()
    {
        // Read arguments from the previous C# step
        CPH.TryGetArg("ptzDirection", out string direction);
        CPH.TryGetArg("ptzDuration", out string durationText);
        direction = (direction ?? "").Trim().ToLowerInvariant();
        // ============================================
        // VALIDATE MOVEMENT DURATION
        // ============================================
        // Supports decimal seconds, e.g. 2.5 or 3.5
        double seconds;
        if (!double.TryParse(durationText, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) || double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0.1 || seconds > MAX_DURATION)
        {
            CPH.SendMessage("PTZ: Duration must be between 0.1 and 30 seconds.");
            return false;
        }

        // Supported PTZ movement and zoom commands
        string[] validDirections =
        {
            "left",
            "right",
            "up",
            "down",
            "leftup",
            "leftdown",
            "rightup",
            "rightdown",
            "zoomin",
            "zoomout"
        };
        // Translate user-friendly Twitch commands
        // into Reolink API operation names.
        if (direction == "zoomin")
            direction = "ZoomInc";
        if (direction == "zoomout")
            direction = "ZoomDec";
        if (Array.IndexOf(validDirections, direction) < 0)
        {
            CPH.SendMessage("PTZ: Invalid movement direction.");
            return false;
        }

        // Accept the camera's local HTTPS certificate
        ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;
        bool movementStarted = false;
        try
        {
            // Check position before moving
            int position = GetPanPosition();
            if (AtBoundary(direction, position))
            {
                CPH.SendMessage("PTZ: Camera is at its pan boundary!");
                return false;
            }

            // ============================================
            // RESET EMERGENCY STOP FOR NEW MOVEMENT
            // ============================================
            // Only reset when the command has passed
            // validation and the initial boundary check.
            CPH.SetGlobalVar("ptzEmergencyStop", false, false);
            // Start camera movement
            MoveCamera(direction);
            movementStarted = true;
            Stopwatch timer = Stopwatch.StartNew();
            // Monitor camera position during movement
            // Stop when the requested duration expires.
            // Supports fractional seconds.
            while (timer.Elapsed.TotalSeconds < seconds)
            {
                // ============================================
                // EMERGENCY STOP CHECK
                // ============================================
                // Read the shared, non-persisted global variable.
                // The separate !ptz stop action sets this to true.
                bool emergencyStop = CPH.GetGlobalVar<bool>("ptzEmergencyStop", false);
                if (emergencyStop)
                {
                    CPH.LogInfo("PTZ: Emergency stop received.");
                    // Exit the monitoring loop.
                    // The finally block will also send Stop.
                    break;
                }

                position = GetPanPosition();
                // Stop if we reach either boundary
                if (AtBoundary(direction, position))
                {
                    CPH.SendMessage("PTZ: Movement stopped at boundary!");
                    break;
                }

                Thread.Sleep(CHECK_INTERVAL);
            }

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("PTZ Controller: " + ex.Message);
            CPH.SendMessage("PTZ: Camera control error. " + "Movement stopped if reachable.");
            return false;
        }
        finally
        {
            // Always attempt to stop the camera.
            // This also runs after a monitoring error.
            try
            {
                StopCamera();
            }
            catch (Exception ex)
            {
                CPH.LogError("PTZ STOP FAILED: " + ex.Message);
            }
        }
    }
}