/* Rosa's Astral Codex */

public Dictionary<long, Asteroid> Asteroids = new Dictionary<long, Asteroid>();

MyIni _storageIni = new MyIni();
MyIni _configIni = new MyIni();
MyIni _replicationIni = new MyIni();

const string ConfigSection = "Astral Codex";

double _raycastRange;
bool _enableRadioReplication;
string _replicationKey;
bool _enableRadarBroadcast;

long _lastAsteroidId;

string _statusMessage = "";

uint _replicationTicks = 0;
IMyBroadcastListener _broadcastListener;

public Program() {
  MyIniParseResult result;
  if (!_configIni.TryParse(Me.CustomData, out result))
      throw new Exception(result.ToString());

  _raycastRange = _configIni.Get(ConfigSection, "raycastRange").ToDouble(15000.0);
  _enableRadioReplication = _configIni.Get(ConfigSection, "enableRadioReplication").ToBoolean(true);
  _replicationKey = _configIni.Get(ConfigSection, "replicationKey").ToString("ASTRALCODEX");
  _enableRadarBroadcast = _configIni.Get(ConfigSection, "enableRadarBroadcast").ToBoolean(true);

  _storageIni.TryParse(Storage);

  List<string> sections = new List<string>();

  _storageIni.GetSections(sections);

  foreach(string section in sections) {
    Asteroid asteroid = new Asteroid(section, _storageIni);


    Asteroids.Add(asteroid.ID, asteroid);
  }

  if (_enableRadioReplication) {
    Runtime.UpdateFrequency = UpdateFrequency.Update10 | UpdateFrequency.Update100;

    _broadcastListener = IGC.RegisterBroadcastListener(_replicationKey);
    _broadcastListener.SetMessageCallback(_replicationKey);
    }
  }

  public void Save() {
  _storageIni.Clear();

  foreach(KeyValuePair<long, Asteroid> entry in Asteroids) {
    entry.Value.AddToIni(_storageIni);
  }

  Storage = _storageIni.ToString();
}

public void Main(string argument, UpdateType updateSource) {
  if (_enableRadioReplication && (updateSource & UpdateType.Update100) != 0) {
    _replicationTicks++;

    if (_replicationTicks > 50) {
      _replicationTicks = 0;

      _replicationIni.Clear();

      foreach(KeyValuePair<long, Asteroid> entry in Asteroids) {
        entry.Value.AddToIni(_replicationIni);
      }

      IGC.SendBroadcastMessage(_replicationKey, _replicationIni.ToString());
    }
  }
  if (_enableRadarBroadcast) {
    foreach(KeyValuePair<long, Asteroid> entry in Asteroids) {
      Asteroid asteroid = entry.Value;
      int radius = (int) asteroid.Diameter / 2;
      byte targetType = (asteroid.ID == _lastAsteroidId) ? (byte) 4 : (byte) 64;

      var data = new MyTuple<byte, long, Vector3D, double>(targetType, asteroid.ID, asteroid.Position, radius * radius);
      IGC.SendBroadcastMessage("IGC_IFF_MSG", data);
    }
  }

  if ((updateSource & UpdateType.IGC) > 0) {
    while (_broadcastListener.HasPendingMessage) {
      MyIGCMessage igcMessage = _broadcastListener.AcceptMessage();
      if (igcMessage.Tag == _replicationKey && igcMessage.Data is string) {
        string replicationData = igcMessage.Data.ToString();

        _replicationIni.TryParse(replicationData);
        List<string> sections = new List<string>();
        _replicationIni.GetSections(sections);

        foreach(string section in sections) {
          Asteroid replicatedAsteroid = new Asteroid(section, _replicationIni);

          if (Asteroids.ContainsKey(replicatedAsteroid.ID)) {
            Asteroid existingAsteroid = Asteroids[replicatedAsteroid.ID];

            if (replicatedAsteroid.LastUpdated > existingAsteroid.LastUpdated) {
              Asteroids[replicatedAsteroid.ID] = replicatedAsteroid;
            }
          } else {
            Asteroids.Add(replicatedAsteroid.ID, replicatedAsteroid);
          }
        }
      }
    }
  }

  string arg = argument.ToLower();

  if (arg == "scan") { Scan(); }
  else if (arg.StartsWith("note ")) { SetNote(argument.Substring(5)); }
  else if (arg.StartsWith("note+ ")) { AddNote(argument.Substring(6)); }
  else if (arg.StartsWith("find ")) { Find(argument.Substring(5)); }
  else if (arg == "go") { GoTo(); }
  else if (arg == "delete") { Delete(); }
  else if (arg == "clear") { Asteroids.Clear(); _lastAsteroidId = 0; }


  if ((updateSource & UpdateType.Update100) != 0) {
    WriteStatusPanels();
    WriteScriptStatus();
  }
}

public void Scan() {
  List<IMyCameraBlock> cameras = new List<IMyCameraBlock>();
  GridTerminalSystem.GetBlocksOfType(cameras, camera => camera.CustomName.Contains("[Codex]"));

  if (cameras.Count == 0) {
    _lastAsteroidId = 0;
    _statusMessage = "Camera not found";
    return;
  }

  IMyCameraBlock cam = cameras.First();

  cam.EnableRaycast = true;

  if (!cam.CanScan(_raycastRange)) {
    _lastAsteroidId = 0;

    int availableRangePercent = (int) Math.Round(100 * cam.AvailableScanRange / _raycastRange, 0);
    _statusMessage = "Scan is still charging (" + availableRangePercent + "% charged)";

    return;
  }

  MyDetectedEntityInfo target = cam.Raycast(_raycastRange);

  if (target.IsEmpty()) {
    _lastAsteroidId = 0;
    _statusMessage = "Asteroid not found.";
    return;
  }

  if (target.Type != MyDetectedEntityType.Asteroid) {
    _lastAsteroidId = 0;
    _statusMessage = "Target is not an asteroid";
    return;
  }

  if (Asteroids.ContainsKey(target.EntityId)) {
    _statusMessage = "Asteroid found in database.";
  } else {
    _statusMessage = "New asteroid discovered!";
    Asteroid asteroid = new Asteroid(target);
    Asteroids.Add(asteroid.ID, asteroid);
  }

  _lastAsteroidId = target.EntityId;
}

public void SetNote(string note) {
  if (_lastAsteroidId == 0) {
    _statusMessage = "Scan an asteroid first to set its note.";
    return;
  }

  _statusMessage = "Note updated.";
  Asteroid asteroid = Asteroids[_lastAsteroidId];
  asteroid.Notes = note;
  asteroid.LastUpdated = DateTime.UtcNow.ToUniversalTime();
  Asteroids[_lastAsteroidId] = asteroid;
}

public void AddNote(string note) {
  if (_lastAsteroidId == 0) {
    _statusMessage = "Scan an asteroid first to set its note.";
    return;
  }

  _statusMessage = "Note updated.";
  Asteroid asteroid = Asteroids[_lastAsteroidId];
  asteroid.Notes = asteroid.Notes + " " + note;
  asteroid.LastUpdated = DateTime.UtcNow.ToUniversalTime();
  Asteroids[_lastAsteroidId] = asteroid;
  }

  public void GoTo() {
  if (_lastAsteroidId == 0) {
    _statusMessage = "Scan or search for an asteroid first to fly to it.";
    return;
  }

  _statusMessage = "Navigating to asteroid.";
  List<IMyRemoteControl> remoteControls = new List<IMyRemoteControl>();
  GridTerminalSystem.GetBlocksOfType(remoteControls);

  IMyRemoteControl rc = remoteControls.First();
  rc.ClearWaypoints();

  Asteroid destination = Asteroids[_lastAsteroidId];
  rc.AddWaypoint(destination.Position, destination.ID.ToString());

  rc.SetCollisionAvoidance(true);
  rc.FlightMode = FlightMode.OneWay;
  rc.SetAutoPilotEnabled(true);
}

public void Find(string query) {
  Vector3D myPos = Me.GetPosition();

  var matches = Asteroids.Where(entry => entry.Value.Notes.Contains(query)).ToList();

  if (matches.Count == 0) {
    _lastAsteroidId = 0;
    _statusMessage = "No matching asteroids found.";
  } else {
    Asteroid closest = matches.MinBy(entry => (float) Vector3D.Distance(myPos, entry.Value.Position)).Value;
    _statusMessage = "Asteroid found.";
    _lastAsteroidId = closest.ID;
  }
}

public void Delete() {
  if (_lastAsteroidId == 0) {
    _statusMessage = "Scan or search for an asteroid first to delete it.";
    return;
  }

  Asteroids.Remove(_lastAsteroidId);
  _lastAsteroidId = 0;
  _statusMessage = "Asteroid deleted from database.";
  }

  public void WriteStatusPanels() {
  List<IMyTerminalBlock> taggedBlocks = new List<IMyTerminalBlock>();
  GridTerminalSystem.GetBlocksOfType(taggedBlocks, block => (block.CustomName.Contains("[Codex]") && (block is IMyTextSurfaceProvider) && (block as IMyTextSurfaceProvider).SurfaceCount > 0));

  foreach (IMyTerminalBlock block in taggedBlocks) {
    int surfaceIndex = 0;
    string mode = "selectedAsteroid";
    string searchTerm = "";

    if (MyIni.HasSection(block.CustomData, "Astral Codex")) {
      MyIniParseResult result;
      if (!_configIni.TryParse(block.CustomData, out result))
          throw new Exception(result.ToString());

      surfaceIndex = _configIni.Get("Astral Codex", "displayPanel").ToInt32(0);
      mode = _configIni.Get("Astral Codex", "mode").ToString("selectedAsteroid");
      searchTerm = _configIni.Get("Astral Codex", "searchTerm").ToString("");
    }

    IMyTextSurfaceProvider surfaceProvider = (block as IMyTextSurfaceProvider);
    IMyTextSurface surface = surfaceProvider.GetSurface(surfaceIndex);

    surface.ContentType = ContentType.TEXT_AND_IMAGE;

    if (mode == "selectedAsteroid") {
      if (_lastAsteroidId != 0) {
        Asteroid asteroid = Asteroids[_lastAsteroidId];

        double distance = Vector3D.Distance(block.GetPosition(), asteroid.Position);
        decimal kilometers = Math.Round((decimal) distance / 1000, 2);

        string message =
          (_statusMessage == "" ? "" : (_statusMessage + "\n")) +
          "Designation: " + asteroid.Designation +
          "\nDistance: " + kilometers + " km" +
          "\nDiameter: " + asteroid.Diameter + " m" +
          "\nNotes: " + (asteroid.Notes == "" ? "(none)" : asteroid.Notes) +
          "\nDiscovered: " + asteroid.Discovered.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") + " UTC" +
          "\nLocation: " + ToGPS(asteroid.Position, asteroid.Designation);

        surface.WriteText(message);
      } else {
        string message =
          (_statusMessage == "" ? "" : (_statusMessage + "\n")) + "Scan an asteroid to view its data.";

        surface.WriteText(message);
      }
    } else if (mode == "searchResults") {
      Vector3D blockPosition = block.GetPosition();
      var gpsList =
        Asteroids
          .Select(entry => entry.Value)
          .Where(asteroid => asteroid.Notes.Contains(searchTerm))
          .Select(asteroid => new MyTuple<string, double>(ToGPS(asteroid.Position, asteroid.Designation), Vector3D.Distance(blockPosition, asteroid.Position)))
          .OrderBy(tuple => tuple.Item2)
          .Select(tuple => Math.Round((decimal) tuple.Item2 / 1000, 2).ToString() + " km - " + tuple.Item1)
          .ToList();

      string text = "Search term: \"" + searchTerm + "\"\n" + string.Join("\n", gpsList);

      surface.WriteText(text);
    }
  }
}

public void WriteScriptStatus() {
  Echo("Rosa's Astral Codex is running...");
  Echo("---");
  Echo("Asteroids in database: " + Asteroids.Count);
  Echo("Maximum scan range: " + _raycastRange + " m");
  Echo("Radio replication: " + (_enableRadioReplication ? "ON" : "OFF"));
  if (_enableRadioReplication) {
    Echo("Replication key: " + _replicationKey);
  }
  Echo("Radar broadcast: " + (_enableRadarBroadcast ? "ON" : "OFF"));
}

public struct Asteroid {
  public Asteroid(MyDetectedEntityInfo info) {
    ID = info.EntityId;
    Designation = GenerateDesignation(info.EntityId);
    Position = info.Position;
    Diameter = info.BoundingBox.Size.Y;
    Notes = "";
    Discovered = DateTime.UtcNow.ToUniversalTime();
    LastUpdated = DateTime.UtcNow.ToUniversalTime();
  }

  public Asteroid(string iniSection, MyIni ini) {
    ID = long.Parse(iniSection);
    Designation = ini.Get(iniSection, "designation").ToString(GenerateDesignation(ID));
    Position = FromGPS(ini.Get(iniSection, "gps").ToString());
    Diameter = ini.Get(iniSection, "diameter").ToDouble(1024);
    Notes = ini.Get(iniSection, "notes").ToString("");
    string discoveredIso8601 = ini.Get(iniSection, "discovered").ToString(DateTime.UtcNow.ToUniversalTime().ToString("o"));
    Discovered = DateTime.Parse(discoveredIso8601);
    string lastUpdatedIso8601 = ini.Get(iniSection, "lastUpdated").ToString(DateTime.UtcNow.ToUniversalTime().ToString("o"));
    LastUpdated = DateTime.Parse(lastUpdatedIso8601);
  }

  public long ID;
  public string Designation;
  public Vector3D Position;
  public double Diameter;
  public string Notes;
  public DateTime Discovered;
  public DateTime LastUpdated;

  public void AddToIni(MyIni ini) {
    string section = ID.ToString();

    ini.Set(section, "designation", Designation);
    ini.Set(section, "gps", ToGPS(Position));
    ini.Set(section, "diameter", Diameter);
    ini.Set(section, "notes", Notes);
    ini.Set(section, "discovered", LastUpdated.ToUniversalTime().ToString("o"));
    ini.Set(section, "lastUpdated", LastUpdated.ToUniversalTime().ToString("o"));
  }
}

static readonly string[] _designations = {
  "Ceres", "Pallas", "Juno", "Vesta", "Astraea", "Hebe", "Iris", "Flora",
  "Metis", "Hygiea", "Parthenope", "Victoria", "Egeria", "Irene", "Eunomia",
  "Psyche", "Thetis", "Melpomeme", "Fortuna", "Massalia", "Lutetia",
  "Kalliope", "Thalia", "Phocaea", "Proserpina", "Euterpe", "Bellona",
  "Amphitrite", "Urania", "Euphrosyne", "Pomona", "Polyhymnia", "Circe",
  "Leukothea", "Atalante", "Fides", "Leda", "Laetitia", "Harmonia", "Daphne",
  "Isis", "Ariadne", "Nysa", "Eugenia", "Hestia", "Aglaja", "Doris", "Pales",
  "Virginia", "Nemausa", "Europa", "Kalypso", "Alexandra", "Pandora", "Melete",
  "Mnemosyne", "Concordia", "Elpis", "Echo", "Danae", "Erato", "Ausonia",
  "Angelina", "Cybele", "Maja", "Asia", "Leto", "Hesperia"
  };

static string GenerateDesignation(long id) {
  long sequenceNumber = id % 9999;
  long designationIndex = id % (_designations.Length - 1);
  string designationProperName = _designations[designationIndex];

  return sequenceNumber.ToString() + " " + designationProperName;
}

static string ToGPS(Vector3D Vec, string Name = "Unknown") {
  return String.Format("GPS:{0}:{1:0.00}:{2:0.00}:{3:0.00}:", Name, Vec.X, Vec.Y, Vec.Z);
}

static Vector3D FromGPS(string GPS) {
  string[] split = GPS.Split(new string[] { ":" }, StringSplitOptions.None);
  return new Vector3D(Convert.ToDouble(split[2]), Convert.ToDouble(split[3]), Convert.ToDouble(split[4]));
}
