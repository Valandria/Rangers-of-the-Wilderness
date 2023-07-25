using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePD.API;
using FivePD.API.Utils;
using MenuAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RangersoftheWilderness.net
{
    [CalloutProperties("Poaching", "HuskyNinja", "v1")]
    internal class NC_Poaching: Callout
    {
        //Utilities
        private CommonFunctions _funcs = new CommonFunctions();
        private Menu _questionMenu;
        private bool[] _questionStatus;
        private Dictionary<string, List<Vector3>> _offsetData = new Dictionary<string, List<Vector3>>();
        private Vector3 _trunkCoords = Vector3.Zero;
        private IPursuit<PursuitStateEnum> _footchase;
        private int _chaseStart = 0;

        //Callout Data
        private Vector3 _coordinates;
        private PedHash _animalHash;
        private PedHash _poacherHash;
        private VehicleHash _vehicleHash;

        private Ped _poacher, _animal;
        private Vehicle _poacherVehicle;

        private PedData _poacherData;
        private VehicleData _poacherVehicleData;

        private List<PedQuestion> _pedQuestions = new List<PedQuestion>();

        public NC_Poaching()
        {
            Init();

            InitInfo(_coordinates);
            ShortName = "Poaching";
            ResponseCode = 2;
            StartDistance = 200f;
            CalloutDescription = $"A tip came in that there are some poachers operating out of {World.GetZoneLocalizedName(_coordinates)}.";
        }
        public override async Task OnAccept()
        {
            InitBlip();
            UpdateData();

            CreateMenu();

            await Task.FromResult(0);
        }
        public override async void OnStart(Ped closest)
        {
            base.OnStart(closest);

            //Spawn Poacher + Animal + Vehicle
            _poacher = await SpawnPed(_poacherHash, _coordinates.Around(15f));
            _animal = await SpawnPed(_animalHash, _coordinates.Around(15f));
            _poacherVehicle = await SpawnVehicle(_vehicleHash, _coordinates.ClosestParkedCarPlacement());

            //Set Atrribute Data
            API.SetEntityHealth(_animal.Handle, 30);
            _poacher.BlockPermanentEvents = true;
            _poacher.AlwaysKeepTask = true;

            //Get Data
            _poacherData = await _poacher.GetData();
            _poacherVehicleData = await _poacherVehicle.GetData();

            //Give the poacher a weapon
            _poacher.Weapons.Give(WeaponHash.Musket, 50, true, true);

            //Set Ped and Vehicle Data
            _poacherData.FirstName = _poacherVehicleData.OwnerFirstName;
            _poacherData.LastName = _poacherVehicleData.OwnerLastName;

            var huntingLicense = new PedData.License();
            huntingLicense.ExpirationDate = _funcs.GetRandomDate("Expired");
            huntingLicense.LicenseStatus = PedData.License.Status.Revoked;
            _poacherData.HuntingLicense = huntingLicense;

            var weaponLicense = new PedData.License();
            weaponLicense.ExpirationDate = _funcs.GetRandomDate("Expired");
            weaponLicense.LicenseStatus = PedData.License.Status.Expired;
            _poacherData.WeaponLicense = weaponLicense;

            //Set data to poacher
            _poacher.SetData(_poacherData);
            _poacher.AttachBlip();
            _animal.AttachBlip();

            //Wait until player is close to the vehicle
            while (World.GetDistance(Game.PlayerPed.Position, _poacher.Position) > 65f) { await BaseScript.Delay(50); }

            _animal.Task.WanderAround();
            _poacher.Task.WanderAround();

            Tick += PoachAnimal;
        }

        //Utility
        private void Init()
        {
            JObject jsonData = _funcs.GetJsonData();

            //----COORDINATE DATA----
            //Get coorindate data for the Poaching Callout
            List<Vector3> coords = new List<Vector3>();

            foreach (var coordinate in jsonData["Poaching-Coordinates"])
            {
                coords.Add(JsonConvert.DeserializeObject<Vector3>(coordinate.ToString()));
            }
            _coordinates = coords.SelectRandom();

            //----ANIMAL DATA----
            Dictionary<string, PedHash> animalHashes = new Dictionary<string, PedHash>();
            string[] animalJSON = JsonConvert.DeserializeObject<string[]>(jsonData["Poaching-Animals"].ToString());

            foreach (string hash in animalJSON)
            {
                int hashKey = API.GetHashKey(hash);

                animalHashes.Add(hash, (PedHash)hashKey);
            }
            _animalHash = animalHashes.SelectRandom().Value;

            //---POACHER DATA----
            Dictionary<string, PedHash> poacherHashes = new Dictionary<string, PedHash>();
            string[] poacherJSON = JsonConvert.DeserializeObject<string[]>(jsonData["Poaching-Poachers"].ToString());
            foreach (string hash in poacherJSON)
            {
                int hashKey = API.GetHashKey(hash);

                poacherHashes.Add(hash, (PedHash)hashKey);
            }
            _poacherHash = poacherHashes.SelectRandom().Value;

            //----VEHICLE DATA----
            Dictionary<string, VehicleHash> vehicleHashes = new Dictionary<string, VehicleHash>();
            string[] vehicleJSON = JsonConvert.DeserializeObject<string[]>(jsonData["Vehicles"].ToString());
            foreach (string hash in vehicleJSON)
            {
                int hashKey = API.GetHashKey(hash);

                vehicleHashes.Add(hash, (VehicleHash)hashKey);
            }
            _vehicleHash = vehicleHashes.SelectRandom().Value;

            //----PED QUESTIONS----
            foreach (var item in jsonData["Poaching-Dialogue"])
            {
                string question = item["Question"].ToString();
                string[] answers = JsonConvert.DeserializeObject<string[]>(item["Answers"].ToString());

                PedQuestion pq = new PedQuestion();
                pq.Question = question;
                pq.Answers = answers.ToList();

                _pedQuestions.Add(pq);
            }

            //----ANIM DATA----
            API.RequestAnimDict("combat@drag_ped@");

            //----CARCASS OFFSETS----
            Vector3 deerPOS = new Vector3(-0.7f, 1.2f, -1f);
            Vector3 deerROT = new Vector3(-200f, 30f, 0f);
            List<Vector3> deerOffsets = new List<Vector3>()
            {
                deerPOS,
                deerROT
            };
            _offsetData.Add("a_c_deer", deerOffsets);

            Vector3 boarPOS = new Vector3(-0.7f, 1.2f, -1.0f);
            Vector3 boarROT = new Vector3(-200f, 0f, 0f);
            List<Vector3> boarOffsets = new List<Vector3>()
            {
                boarPOS,
                boarROT
            };
            _offsetData.Add("a_c_boar", boarOffsets);

            Vector3 mtlionPOS = new Vector3(0.1f, 0.7f, -1.0f);
            Vector3 mtlionROT = new Vector3(-210f, 0f, 0f);
            List<Vector3> mtlionOffsets = new List<Vector3>()
            {
                mtlionPOS,
                mtlionROT
            };
            _offsetData.Add("a_c_mtlion", mtlionOffsets);

            Vector3 coyotePOS = new Vector3(-0.2f, 0.15f, 0.45f);
            Vector3 coyoteROT = new Vector3(0f, -90f, 0f);
            List<Vector3> coyoteOffsets = new List<Vector3>()
            {
                coyotePOS,
                coyoteROT
            };
            _offsetData.Add("a_c_coyote", coyoteOffsets);
        }
        private void CreateMenu()
        {
            _questionMenu = new Menu("Ped Questions", "Ped Questions");
            string[] selectedAnswers = new string[_pedQuestions.Count];
            _questionStatus = new bool[_pedQuestions.Count];

            foreach (PedQuestion q in _pedQuestions)
            {
                _questionMenu.AddMenuItem(new MenuItem("~y~" + q.Question));
                selectedAnswers[_pedQuestions.IndexOf(q)] = q.Answers.SelectRandom();
            }

            _questionMenu.OnItemSelect += (_menu, _item, _index) =>
            {
                _funcs.ShowSubtitle(selectedAnswers[_index], 5000);
                _item.Text = "~c~" + _item.Text.Substring(3);

                //Question answered for first time
                if (!_questionStatus[_index])
                {
                    _questionStatus[_index] = true;
                }
            };

            MenuController.AddMenu(_questionMenu);
        }

        //On Tick Logic
        private async Task PoachAnimal()
        {
            if (World.GetDistance(_animal.Position, _poacher.Position) >= 15f) { _poacher.Task.GoTo(_animal); await BaseScript.Delay(5000); return; }
            _animal.Task.ClearAllImmediately();
            _poacher.Task.ClearAllImmediately();

            await BaseScript.Delay(500);

            _poacher.Task.ShootAt(_animal);

            while (_animal.IsAlive) { await BaseScript.Delay(50); }

            _poacher.Task.ClearAllImmediately();
            _poacher.Task.GoTo(_animal, new Vector3(1f, 1f, 0));

            Tick -= PoachAnimal;
            CheckAnimal();

            await Task.FromResult(0);
        }
        private async Task QuestionMenu()
        {
            //Check if player is close
            if (World.GetDistance(Game.PlayerPed.Position, _poacher.Position) > 2f) { return; }
            if (!_poacher.IsStopped) { return; }

            //If the menu is up dont draw text
            if (!_questionMenu.Visible)
            {
                _funcs.Draw3dText("~y~Press~s~ ~r~[H]~s~ to ~b~Question Poacher", _poacher.Position);

                if (Game.IsControlPressed(0, (Control)74))
                {
                    _questionMenu.Visible = true;
                }
            }

            //All Questions have been answered, Poacher is CUffed and is in the vehicle
            //Time to remove this tick
            if (_questionStatus.All(x => true) && _poacher.IsInVehicle())
            {
                Tick -= QuestionMenu;
                Tick += RemoveAnimalInit;
            }

            await Task.FromResult(0);
        }
        private async Task RemoveAnimalInit()
        {
            if (!_animal.Exists()) { Tick -= RemoveAnimalInit; return; }

            if (World.GetDistance(Game.PlayerPed.Position, _animal.Position) > 1.5f) { return; }
            _funcs.Draw3dText("~y~Press~s~ ~r~[H]~s~ to ~b~Take Animal", new Vector3(_animal.Position.X, _animal.Position.Y, _animal.Position.Z + 0.5f));

            if (Game.IsControlJustPressed(0, (Control)74))
            {
                Tick += PutAnimalInTrunk;
                _trunkCoords = API.GetWorldPositionOfEntityBone(Game.PlayerPed.LastVehicle.Handle, API.GetEntityBoneIndexByName(Game.PlayerPed.LastVehicle.Handle, "boot"));

                if (_animal.Model.Hash == API.GetHashKey("a_c_coyote"))
                {
                    Game.PlayerPed.Task.PlayAnimation("missfinale_c2mcs_1", "fin_c2_mcs_1_camman", 8.0f, -1, (AnimationFlags)49);
                }
                else
                {
                    Game.PlayerPed.Task.PlayAnimation("combat@drag_ped@", "injured_drag_plyr", 8.0f, -1, (AnimationFlags)1);
                    Tick += DragMovement;
                }

                Tick -= RemoveAnimalInit;
                AttachAnimal();
            }

            await Task.FromResult(0);
        }
        private async Task DragMovement()
        {
            if (Game.IsControlPressed(0, (Control)35))
            {
                API.FreezeEntityPosition(Game.PlayerPed.Handle, false);
                Game.PlayerPed.Heading += 1.5f;
            }
            else if (Game.IsControlPressed(0, (Control)34))
            {
                API.FreezeEntityPosition(Game.PlayerPed.Handle, false);
                Game.PlayerPed.Heading -= 1.5f;
            }
            else if (Game.IsControlPressed(0, (Control)32) || Game.IsControlPressed(0, (Control)33))
            {
                API.FreezeEntityPosition(Game.PlayerPed.Handle, false);
            }
            else
            {
                API.FreezeEntityPosition(Game.PlayerPed.Handle, true);
                Game.PlayerPed.Task.PlayAnimation("combat@drag_ped@", "injured_drag_plyr", 8.0f, -1, (AnimationFlags)1);
            }

            await Task.FromResult(0);
        }
        private async Task PutAnimalInTrunk()
        {
            if (World.GetDistance(Game.PlayerPed.Position, _trunkCoords) > 1.5f) { return; }
            _funcs.Draw3dText("~y~Press~s~ ~r~[H]~s~ to ~b~Store Animal~s~", _trunkCoords);

            if (Game.IsControlJustPressed(0, (Control)74))
            {
                Tick -= PutAnimalInTrunk;
                if (_animal.Model.Hash != API.GetHashKey("a_c_coyote"))
                {
                    Tick -= DragMovement;
                }

                Game.PlayerPed.LastVehicle.Doors[VehicleDoorIndex.Trunk].Open(false, true);
                _animal.Delete();
                _animal = null;

                await BaseScript.Delay(750);
                Game.PlayerPed.LastVehicle.Doors[VehicleDoorIndex.Trunk].Close(false);

                Game.PlayerPed.Task.ClearAll();
                API.FreezeEntityPosition(Game.PlayerPed.Handle, false);
            }

            await Task.FromResult(0);
        }
        private async Task FootChase()
        {
            if (API.GetGameTimer() - _chaseStart <= 8000)
            {
                API.SetPedMoveRateOverride(_poacher.Handle, 1.25f);
                return;
            }

            if (_poacher.IsCuffed)
            {
                try
                {
                    _footchase.Terminate();
                }
                catch
                {
                    _footchase = null;
                }

                Tick -= FootChase;
                Tick += QuestionMenu;
            }

            await Task.FromResult(0);
        }

        //Callout Functions
        private async void CheckAnimal()
        {
            List<string> idles = new List<string>() { "idle_a", "idle_b", "idle_c" };

            string enterDict = "amb@medic@standing@kneel@enter";
            string enterName = "enter";

            string exitDict = "amb@medic@standing@tendtodead@exit";
            string exitName = "exit";

            string idleDict = "amb@medic@standing@tendtodead@idle_a";
            string idleName = idles.SelectRandom();

            API.RequestAnimDict(enterDict);
            while (!API.HasAnimDictLoaded(enterDict)) { await BaseScript.Delay(10); }

            API.RequestAnimDict(exitDict);
            while (!API.HasAnimDictLoaded(exitDict)) { await BaseScript.Delay(10); }

            API.RequestAnimDict(idleDict);
            while (!API.HasAnimDictLoaded(idleDict)) { await BaseScript.Delay(10); }

            TaskSequence ts = new TaskSequence();
            ts.AddTask.PlayAnimation(enterDict, enterName);
            ts.AddTask.PlayAnimation(idleDict, idleName, 8.0f, -1, AnimationFlags.Loop);
            ts.Close();

            _poacher.Task.PerformSequence(ts);
            while (World.GetDistance(Game.PlayerPed.Position, _poacher.Position) > 9f) { await BaseScript.Delay(10); }

            if (new Random().Next(0, 100) >= 50)
            {
                _funcs.ShowSubtitle("~y~Poacher~s~: It's the fuzz! I'm outta here!", 5000);

                _footchase = Pursuit.RegisterPursuit(_poacher);
                _footchase.Init(false, 15f, 15f);
                _footchase.ActivatePursuit();

                _chaseStart = API.GetGameTimer();
                Tick += FootChase;
            }
            else
            {
                _poacher.Task.PlayAnimation(exitDict, exitName);
                await BaseScript.Delay(950);
                _poacher.Task.TurnTo(Game.PlayerPed);
                Tick += QuestionMenu;
            }
        }
        private void AttachAnimal()
        {
            Vector3 playerPos = Game.PlayerPed.Position;
            List<Vector3> offsets = new List<Vector3>();

            foreach (KeyValuePair<string, List<Vector3>> entry in _offsetData)
            {
                if (API.GetHashKey(entry.Key) == _animal.Model.Hash)
                {
                    offsets = entry.Value;
                }
            }

            Model animalModel = _animal.Model;

            _animal.Delete();
            _animal = null;

            int animal = API.CreatePed(1, (uint)animalModel.Hash, playerPos.X, playerPos.Y, playerPos.Z, Game.PlayerPed.Heading, true, true);
            _animal = new Ped(animal);

            API.SetEntityInvincible(_animal.Handle, true);
            API.SetEntityHealth(_animal.Handle, 0);

            API.AttachEntityToEntity(_animal.Handle, Game.PlayerPed.Handle, 11816, offsets[0].X, offsets[0].Y, offsets[0].Z, offsets[1].X, offsets[1].Y, offsets[1].Z, false, false, false, true, 2, true);
        }
    }
}
