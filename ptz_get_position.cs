
using System;
using System.Net;
using System.Text.RegularExpressions;

public class CPHInline
{
    public bool Execute()
    {
        string url = "https://IP_GOES_HERE/cgi-bin/api.cgi"
            + "?user=USERNAME_HERE"
            + "&password=PASSWORD_HERE";

        string json =
            "[{\"cmd\":\"GetPtzCurPos\",\"action\":1,"
            + "\"param\":{\"PtzCurPos\":{\"channel\":0}}}]";

        try
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.ContentType] =
                    "application/json";

                ServicePointManager.ServerCertificateValidationCallback =
                    (sender, cert, chain, errors) => true;

                string response = client.UploadString(
                    url, "POST", json);

                Match match = Regex.Match(
                    response, "\"Ppos\"\\s*:\\s*(\\d+)");

                if (!match.Success)
                {
                    CPH.LogError("PTZ: No pan position returned");
                    return false;
                }

                int panPosition = int.Parse(
                    match.Groups[1].Value);

                CPH.SetArgument("ptzPosition", panPosition);
				CPH.SetArgument("ptzAllowed", true);
				int leftLimit = 1376;
				int rightLimit = 1097;

				string direction = args["ptzDirection"].ToString().ToLower();

				bool movingLeft = direction.Contains("left");
				bool movingRight = direction.Contains("right");

				
				if (movingLeft && panPosition >= leftLimit)
				{
					CPH.SetArgument("ptzAllowed", false);
					CPH.SendMessage("PTZ: Camera is at its left boundary!");
					return false;
				}

				if (movingRight && panPosition <= rightLimit)
				{
					CPH.SetArgument("ptzAllowed", false);
					CPH.SendMessage("PTZ: Camera is at its right boundary!");
					return false;
				}


                CPH.LogInfo(
                    $"PTZ current pan position: {panPosition}");

                return true;
            }
        }
        catch (Exception ex)
        {
            CPH.LogError("PTZ position error: " + ex.Message);
            return false;
        }
    }
}
