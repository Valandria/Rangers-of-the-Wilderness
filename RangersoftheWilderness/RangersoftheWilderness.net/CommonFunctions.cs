using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RangersoftheWilderness.net
{
    internal class CommonFunctions
    {
        private Random _rng = new Random();

        public CommonFunctions()
        {

        }

        public JObject GetJsonData()
        {
            //Load coordinates file into a string
            string path = "/callouts/WildlifeCalloutPack/data.json";
            string data = API.LoadResourceFile(API.GetCurrentResourceName(), path);

            //Parse the string into json
            JObject json = JObject.Parse(data);

            return json;
        }
        public string GetRandomDate(string flag)
        {
            switch (flag)
            {
                case "Expired":
                    return $"{_rng.Next(1, 31)}/{_rng.Next(1, 13)}/20{_rng.Next(15, 23)}";

                default:
                    return "06/09/1969";
            }
        }
        public void ShowSubtitle(string msg, int duration)
        {
            API.BeginTextCommandPrint("STRING");
            API.AddTextComponentString(msg);
            API.EndTextCommandPrint(duration, false);
        }
        public void Draw3dText(string msg, Vector3 pos)
        {
            float textX = 0f, textY = 0f;
            Vector3 camLoc;
            API.World3dToScreen2d(pos.X, pos.Y, pos.Z, ref textX, ref textY);
            camLoc = API.GetGameplayCamCoords();
            float distance = API.GetDistanceBetweenCoords(camLoc.X, camLoc.Y, camLoc.Z, pos.X, pos.Y, pos.Z, true);
            float scale = (1 / distance) * 2;
            float fov = (1 / API.GetGameplayCamFov()) * 100;
            scale = scale * fov * 0.5f;

            API.SetTextScale(0.0f, scale);
            API.SetTextFont(0);
            API.SetTextProportional(true);
            API.SetTextColour(255, 255, 255, 215);
            API.SetTextDropshadow(0, 0, 0, 0, 255);
            API.SetTextEdge(2, 0, 0, 0, 150);
            API.SetTextDropShadow();
            API.SetTextOutline();
            API.SetTextEntry("STRING");
            API.SetTextCentre(true);
            API.AddTextComponentString(msg);
            API.DrawText(textX, textY);
        }
    }
}
