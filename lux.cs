/* Rosa's Light Manager */

public Dictionary<string, LightSettings> _settings = new Dictionary<string, LightSettings>();
public List<IMyLightingBlock> _taggedLights = new List<IMyLightingBlock>();
public List<IMyLightingBlock> _groupLights = new List<IMyLightingBlock>();
public Dictionary<string, int> _groupSizes = new Dictionary<string, int>();

MyIni _configIni = new MyIni();

public Program() {
  Runtime.UpdateFrequency = UpdateFrequency.Update100;
}

public void Save() {}

public void Main(string argument, UpdateType updateSource) {
    MyIniParseResult result;
  if (!_configIni.TryParse(Me.CustomData, out result))
      throw new Exception(result.ToString());

  List<string> sections = new List<string>();

  _configIni.GetSections(sections);
  List<string> tags = sections.Where(section => section.StartsWith("RLM:")).ToList();

  _settings.Clear();
  foreach (string tag in tags) {
    LightSettings settings = new LightSettings(tag, _configIni);
    _settings.Add(tag, settings);
  }

  _groupSizes.Clear();

  GridTerminalSystem.GetBlocksOfType(_taggedLights, block => block.IsSameConstructAs(Me) && block.CustomName.Contains("[RLM:"));

  foreach (KeyValuePair<string, LightSettings> lightSettings in _settings) {
    _groupSizes.TryAdd(lightSettings.Key.Substring(4), 0);
    IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(lightSettings.Key.Substring(4));
    if (group != null) {
      group.GetBlocksOfType<IMyLightingBlock>(_groupLights);

      foreach (IMyLightingBlock light in _groupLights) {
        lightSettings.Value.ApplyTo(light);
      }

      _groupSizes[lightSettings.Key.Substring(4)] += _groupLights.Count;
    }

    string nameTag = "[" + lightSettings.Key + "]";
    List<IMyLightingBlock> taggedLights = _taggedLights.Where(light => light.CustomName.Contains(nameTag)).ToList();

    foreach (IMyLightingBlock light in taggedLights) {
      lightSettings.Value.ApplyTo(light);
    }

    _groupSizes[lightSettings.Key.Substring(4)] += taggedLights.Count;
  }

  Echo("Lux Rosa");

  if (_settings.Count == 0) { Echo("No lighting profiles configured. Add them to this block's Custom Data."); }
  else if (_settings.Count == 1) { Echo("Managing 1 lighting profile."); }
  else if (_settings.Count > 1) { Echo("Managing " + _settings.Count + " lighting profiles."); }

  foreach(KeyValuePair<string, int> groupSize in _groupSizes) {
    Echo(groupSize.Key + ": " + groupSize.Value + " lights");
  }
}

public struct LightSettings {
  public LightSettings(string tag, MyIni config) {
    MyIniValue r = config.Get(tag, "r");
    MyIniValue g = config.Get(tag, "g");
    MyIniValue b = config.Get(tag, "b");

    if (r.IsEmpty || g.IsEmpty || b.IsEmpty) {
      Color = null;
    } else {
      Color = new Color(r.ToInt32(), g.ToInt32(), b.ToInt32());
    }

    Radius = GetConfigDouble(config, tag, "radius");
    Falloff = GetConfigDouble(config, tag, "falloff");
    Intensity = GetConfigDouble(config, tag, "intensity");
    BlinkIntervalSeconds = GetConfigDouble(config, tag, "blinkIntervalSeconds");
    BlinkLength = GetConfigDouble(config, tag, "blinkLength");
    BlinkOffset = GetConfigDouble(config, tag, "blinkOffset");
  }

  public Color? Color;
  public double? Radius;
  public double? Falloff;
  public double? Intensity;
  public double? BlinkIntervalSeconds;
  public double? BlinkLength;
  public double? BlinkOffset;

  public void ApplyTo(IMyLightingBlock light) {
    if (Color.HasValue) {
      light.Color = Color.Value;
    }

    if (Radius.HasValue) {
      light.Radius = (float) Radius.Value;
    }

    if (Falloff.HasValue) {
      light.Falloff = (float) Falloff.Value;
    }

    if (Intensity.HasValue) {
      light.Intensity = (float) Intensity.Value;
    }

    if (BlinkLength.HasValue) {
      light.BlinkLength = (float) BlinkLength.Value;
    }

    if (BlinkIntervalSeconds.HasValue) {
      light.BlinkIntervalSeconds = (float) BlinkIntervalSeconds.Value;
    }

    if (BlinkOffset.HasValue) {
      light.BlinkOffset = (float) BlinkOffset.Value;
    }
  }
}

public static double? GetConfigDouble(MyIni config, string tag, string key) {
  MyIniValue configValue = config.Get(tag, key);

  if (configValue.IsEmpty) {
    return null;
  } else {
    return configValue.ToDouble();
  }
}
