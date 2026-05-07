using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NPCMaker;

public partial class MainWindow : Window
{
    private TextBox _npcInternalNameBox;
    private TextBox _npcDisplayNameBox;
    private TextBox _authorBox;
    private Avalonia.Controls.Image _portraitPreview;
    private Avalonia.Controls.Image _spritePreview;
    private TextBox _portraitPathBox;
    private TextBox _spritePathBox;
    private TextBlock _statusText;
    // Wizard / page controls
    private StackPanel _page1;
    private StackPanel _page2;
    private Button _nextButton;
    private Button _backButton;
    private Button _aboutButton;
    private ScrollViewer _pageImage;
    private ScrollViewer _pageAbout;

    // Dialogue controls
    private TextBox _dialogueKeyBox;
    private TextBox _dialogueValueBox;
    private Button _addDialogueButton;
    private ListBox _dialoguesListBox;
    private List<KeyValuePair<string, string>> _dialogues = new();

    // Schedule controls
    private Button _addEventButton;
    private ListBox _eventsListBox;
    private List<KeyValuePair<string, string>> _events = new();
    private TextBox _seasonBox;
    private TextBox _timeRangeBox;
    private TextBox _locationBox;
    private TextBox _xBox;
    private TextBox _yBox;
    private TextBox _zBox;
    private ComboBox _directionBox;
    private Button _addScheduleButton;
    private ListBox _schedulesListBox;
    private List<KeyValuePair<string, string>> _schedules = new();

    // Disposition controls
    private ScrollViewer _page3;
    private ScrollViewer _page4;
    private int _previousPage = 1;
    private ComboBox _ageCombo;
    private ComboBox _mannerCombo;
    private ComboBox _personalityCombo;
    private ComboBox _optimismCombo;
    private ComboBox _genderCombo;
    private ComboBox _breathingCombo;
    private TextBox _homeBox;
    private ComboBox _datableCombo;
    private TextBox _relationshipBox;
    private TextBox _spawnMapBox;
    private TextBox _spawnXBox;
    private TextBox _spawnYBox;
    private TextBox _lovesBox;
    private TextBox _likesBox;
    private TextBox _dislikesBox;
    private TextBox _hatesBox;

    public MainWindow()
    {
        InitializeComponent();

        _npcInternalNameBox = this.FindControl<TextBox>("NpcInternalNameBox")!;
        _npcDisplayNameBox = this.FindControl<TextBox>("NpcDisplayNameBox")!;
        _authorBox = this.FindControl<TextBox>("AuthorBox")!;
    _portraitPathBox = this.FindControl<TextBox>("PortraitPathBox")!;
    _spritePathBox = this.FindControl<TextBox>("SpritePathBox")!;
    _statusText = this.FindControl<TextBlock>("StatusText")!;

    // pages
        _page1 = this.FindControl<StackPanel>("Page1")!;
        _page2 = this.FindControl<StackPanel>("Page2")!;
        _pageImage = this.FindControl<ScrollViewer>("PageImage")!;
        _page4 = this.FindControl<ScrollViewer>("Page4")!;
        _pageAbout = this.FindControl<ScrollViewer>("PageAbout")!;

        // previews
        _portraitPreview = this.FindControl<Avalonia.Controls.Image>("PortraitPreview")!;
        _spritePreview = this.FindControl<Avalonia.Controls.Image>("SpritePreview")!;

    // navigation
    _nextButton = this.FindControl<Button>("NextButton")!;
    _backButton = this.FindControl<Button>("BackButton")!;

    // dialogue controls
    _dialogueKeyBox = this.FindControl<TextBox>("DialogueKeyBox")!;
    _dialogueValueBox = this.FindControl<TextBox>("DialogueValueBox")!;
    _addDialogueButton = this.FindControl<Button>("AddDialogueButton")!;
    _dialoguesListBox = this.FindControl<ListBox>("DialoguesListBox")!;

    // schedule controls
    _seasonBox = this.FindControl<TextBox>("SeasonBox")!;
    _timeRangeBox = this.FindControl<TextBox>("TimeRangeBox")!;
    _locationBox = this.FindControl<TextBox>("LocationBox")!;
    _xBox = this.FindControl<TextBox>("XBox")!;
    _yBox = this.FindControl<TextBox>("YBox")!;
    _zBox = this.FindControl<TextBox>("ZBox")!;
    _directionBox = this.FindControl<ComboBox>("DirectionBox")!;
    _addScheduleButton = this.FindControl<Button>("AddScheduleButton")!;
    _schedulesListBox = this.FindControl<ListBox>("SchedulesListBox")!;
    _addEventButton = this.FindControl<Button>("AddEventButton")!;
    _eventsListBox = this.FindControl<ListBox>("EventsListBox")!;

    // disposition controls
    _page3 = this.FindControl<ScrollViewer>("Page3")!;
    _ageCombo = this.FindControl<ComboBox>("AgeCombo")!;
    _mannerCombo = this.FindControl<ComboBox>("MannerCombo")!;
    _personalityCombo = this.FindControl<ComboBox>("PersonalityCombo")!;
    _optimismCombo = this.FindControl<ComboBox>("OptimismCombo")!;
    _genderCombo = this.FindControl<ComboBox>("GenderCombo")!;
    _breathingCombo = this.FindControl<ComboBox>("BreathingCombo")!;
    _homeBox = this.FindControl<TextBox>("HomeBox")!;
    _datableCombo = this.FindControl<ComboBox>("DatableCombo")!;
    _relationshipBox = this.FindControl<TextBox>("RelationshipBox")!;
    _spawnMapBox = this.FindControl<TextBox>("SpawnMapBox")!;
    _spawnXBox = this.FindControl<TextBox>("SpawnXBox")!;
    _spawnYBox = this.FindControl<TextBox>("SpawnYBox")!;
    _lovesBox = this.FindControl<TextBox>("LovesBox")!;
    _likesBox = this.FindControl<TextBox>("LikesBox")!;
    _dislikesBox = this.FindControl<TextBox>("DislikesBox")!;
    _hatesBox = this.FindControl<TextBox>("HatesBox")!;

    // wire events
    this.FindControl<Button>("PortraitBrowseButton")!.Click += BrowsePortrait_Click;
    this.FindControl<Button>("SpriteBrowseButton")!.Click += BrowseSprite_Click;
    this.FindControl<Button>("GenerateButton")!.Click += Generate_Click;
    _aboutButton = this.FindControl<Button>("AboutButton")!;
    _aboutButton.Click += AboutButton_Click;
    _nextButton.Click += NextButton_Click;
    _backButton.Click += BackButton_Click;
    _addDialogueButton.Click += AddDialogue_Click;
    _addScheduleButton.Click += AddSchedule_Click;
    _addEventButton.Click += AddEvent_Click;
    }

    private void AboutButton_Click(object? sender, RoutedEventArgs e)
    {
        // show About page and remember where we came from
        _previousPage = _currentPage;
        // hide all wizard pages
        _page1.IsVisible = false;
        _pageImage.IsVisible = false;
        _page2.IsVisible = false;
        _page3.IsVisible = false;
        _page4.IsVisible = false;
        // show about
        _pageAbout.IsVisible = true;
        _currentPage = 99; // sentinel for about
        _backButton.IsEnabled = true;
        _nextButton.IsEnabled = false;
    }

    private async void BrowsePortrait_Click(object? sender, RoutedEventArgs e)
    {
        var path = await GetFilePathAsync();
        if (path != null)
        {
            _portraitPathBox.Text = path;
            try
            {
                var bmp = new Avalonia.Media.Imaging.Bitmap(path);
                _portraitPreview.Source = bmp;
            }
            catch { }
        }
    }

    private async void BrowseSprite_Click(object? sender, RoutedEventArgs e)
    {
        var path = await GetFilePathAsync();
        if (path != null)
        {
            _spritePathBox.Text = path;
            try
            {
                var bmp = new Avalonia.Media.Imaging.Bitmap(path);
                _spritePreview.Source = bmp;
            }
            catch { }
        }
    }

    private void Generate_Click(object? sender, RoutedEventArgs e)
    {
        _statusText.Text = "Generating...";

        try
        {
            var inputData = new NpcInputData
            {
                NpcInternalName = _npcInternalNameBox.Text ?? "",
                NpcDisplayName = _npcDisplayNameBox.Text ?? "",
                Author = _authorBox.Text ?? "",
                SourcePortraitPath = _portraitPathBox.Text ?? "",
                SourceSpritePath = _spritePathBox.Text ?? ""
            };

            if (string.IsNullOrWhiteSpace(inputData.NpcInternalName) ||
                string.IsNullOrWhiteSpace(inputData.NpcDisplayName) ||
                string.IsNullOrWhiteSpace(inputData.Author) ||
                string.IsNullOrWhiteSpace(inputData.SourcePortraitPath) ||
                string.IsNullOrWhiteSpace(inputData.SourceSpritePath))
            {
                _statusText.Text = "Error: Please fill all fields.";
                return;
            }

            var generator = new NpcGeneratorService();
            
            // Collect disposition data
            var disposition = new NpcDisposition
            {
                Age = ((_ageCombo.SelectedItem as ComboBoxItem)?.Content ?? "adult").ToString() ?? "adult",
                Manner = ((_mannerCombo.SelectedItem as ComboBoxItem)?.Content ?? "neutral").ToString() ?? "neutral",
                Personality = ((_personalityCombo.SelectedItem as ComboBoxItem)?.Content ?? "neutral").ToString() ?? "neutral",
                Optimism = ((_optimismCombo.SelectedItem as ComboBoxItem)?.Content ?? "0").ToString() ?? "0",
                Gender = ((_genderCombo.SelectedItem as ComboBoxItem)?.Content ?? "0 (Male)").ToString() ?? "0 (Male)",
                Breathing = ((_breathingCombo.SelectedItem as ComboBoxItem)?.Content ?? "0").ToString() ?? "0",
                Home = _homeBox.Text ?? "Town",
                Datable = ((_datableCombo.SelectedItem as ComboBoxItem)?.Content ?? "datable").ToString() ?? "datable",
                Relationship = _relationshipBox.Text ?? "null",
                SpawnMap = _spawnMapBox.Text ?? "Town",
                SpawnX = int.TryParse(_spawnXBox.Text, out int sx) ? sx : 1,
                SpawnY = int.TryParse(_spawnYBox.Text, out int sy) ? sy : 1,
                Loves = _lovesBox.Text ?? "(74)",
                Likes = _likesBox.Text ?? "(206)",
                Dislikes = _dislikesBox.Text ?? "(168)",
                Hates = _hatesBox.Text ?? "(172)"
            };

            string outputDir = generator.Generate(inputData, _dialogues, _schedules, disposition, _events);

            _statusText.Text = $"Success! Mod generated in: {outputDir}";
        }
        catch (Exception ex)
        {
            _statusText.Text = $"Error: {ex.Message}";
        }
    }

    private async System.Threading.Tasks.Task<string?> GetFilePathAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return null;

        var filePickerOptions = new FilePickerOpenOptions
        {
            Title = "Select Image",
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImagePng }
        };

        var result = await topLevel.StorageProvider.OpenFilePickerAsync(filePickerOptions);

        if (result.Count > 0)
        {
            var filePath = result[0].Path.LocalPath;
            return filePath;
        }
        return null;
    }

    private int _currentPage = 1; // Track which page we're on

    private void NextButton_Click(object? sender, RoutedEventArgs e)
    {
        // Page flow: 1 -> Image -> 2 -> 3 -> 4
        if (_currentPage == 1)
        {
            _page1.IsVisible = false;
            _pageImage.IsVisible = true;
            _backButton.IsEnabled = true;
            _currentPage = 2;
        }
        else if (_currentPage == 2)
        {
            _pageImage.IsVisible = false;
            _page2.IsVisible = true;
            _currentPage = 3;
        }
        else if (_currentPage == 3)
        {
            _page2.IsVisible = false;
            _page3.IsVisible = true;
            _currentPage = 4;
        }
        else if (_currentPage == 4)
        {
            _page3.IsVisible = false;
            _page4.IsVisible = true;
            _nextButton.IsEnabled = false;
            _currentPage = 5;
        }
    }

    private void BackButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentPage == 99)
        {
            // return from About to previous page
            _pageAbout.IsVisible = false;
            // restore previous page visibility
            if (_previousPage == 1) _page1.IsVisible = true;
            else if (_previousPage == 2) _pageImage.IsVisible = true;
            else if (_previousPage == 3) _page2.IsVisible = true;
            else if (_previousPage == 4) _page3.IsVisible = true;
            else if (_previousPage == 5) _page4.IsVisible = true;
            _currentPage = _previousPage;
            // restore next button state
            _nextButton.IsEnabled = _currentPage != 5;
            _backButton.IsEnabled = _currentPage != 1;
            return;
        }
        // Back flow for pages 1..5
        if (_currentPage == 2)
        {
            // from Image back to Page1
            _page1.IsVisible = true;
            _pageImage.IsVisible = false;
            _backButton.IsEnabled = false;
            _nextButton.IsEnabled = true;
            _currentPage = 1;
        }
        else if (_currentPage == 3)
        {
            // from Page2 back to Image
            _pageImage.IsVisible = true;
            _page2.IsVisible = false;
            _currentPage = 2;
        }
        else if (_currentPage == 4)
        {
            // from Page3 back to Page2
            _page2.IsVisible = true;
            _page3.IsVisible = false;
            _currentPage = 3;
        }
        else if (_currentPage == 5)
        {
            // from Events back to Page3
            _page3.IsVisible = true;
            _page4.IsVisible = false;
            _nextButton.IsEnabled = true;
            _currentPage = 4;
        }
    }

    private void AddDialogue_Click(object? sender, RoutedEventArgs e)
    {
        var key = _dialogueKeyBox.Text?.Trim();
        var val = _dialogueValueBox.Text?.Trim();
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) return;
        _dialogues.Add(new KeyValuePair<string, string>(key, val));
        var dlgItems = _dialoguesListBox.Items as System.Collections.IList;
        dlgItems?.Clear();
        foreach (var kv in _dialogues)
            dlgItems?.Add($"{kv.Key}: {kv.Value}");
        _dialogueKeyBox.Text = "";
        _dialogueValueBox.Text = "";
    }

    private void AddSchedule_Click(object? sender, RoutedEventArgs e)
    {
        var season = _seasonBox.Text?.Trim() ?? "spring";
        var timeRange = _timeRangeBox.Text?.Trim();
        var location = _locationBox.Text?.Trim() ?? "Town";
        var x = _xBox.Text?.Trim() ?? "0";
        var y = _yBox.Text?.Trim() ?? "0";
        var z = _zBox.Text?.Trim() ?? "0";
        var direction = ((_directionBox.SelectedItem as ComboBoxItem)?.Content ?? "0").ToString()?.Split(' ')[0] ?? "0";
        if (string.IsNullOrEmpty(timeRange)) return;

        // store schedule as season|timeRange|location|x|y|z|direction
        var value = $"{timeRange}|{location}|{x}|{y}|{z}|{direction}";
        _schedules.Add(new KeyValuePair<string, string>(season, value));
        var schItems = _schedulesListBox.Items as System.Collections.IList;
        schItems?.Clear();
        foreach (var s in _schedules)
            schItems?.Add($"{s.Key}: {s.Value}");

        _seasonBox.Text = "";
        _timeRangeBox.Text = "";
        _locationBox.Text = "";
        _xBox.Text = "";
        _yBox.Text = "";
        _zBox.Text = "";
        _directionBox.SelectedIndex = 0;
    }

    private void AddEvent_Click(object? sender, RoutedEventArgs e)
    {
        var keyBox = this.FindControl<TextBox>("EventKeyBox");
        var valueBox = this.FindControl<TextBox>("EventValueBox");
        if (keyBox == null || valueBox == null) return;
        var key = keyBox.Text?.Trim();
        var val = valueBox.Text?.Trim();
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(val)) return;
        _events.Add(new KeyValuePair<string, string>(key, val));
        var items = _eventsListBox.Items as System.Collections.IList;
        items?.Clear();
        foreach (var kv in _events)
            items?.Add($"{kv.Key}: {kv.Value}");
        keyBox.Text = string.Empty;
        valueBox.Text = string.Empty;
    }
}


public class NpcInputData
{
    public string NpcInternalName { get; set; } = "";
    public string NpcDisplayName { get; set; } = "";
    public string Author { get; set; } = "";
    public string SourcePortraitPath { get; set; } = "";
    public string SourceSpritePath { get; set; } = "";
}

public class NpcDisposition
{
    // Fields 1-15 in NPCDispositions
    public string Age { get; set; } = "adult";                 // Field 1
    public string Manner { get; set; } = "neutral";            // Field 2
    public string Personality { get; set; } = "neutral";       // Field 3
    public string Optimism { get; set; } = "0";                // Field 4
    public string Gender { get; set; } = "0";                  // Field 5
    public string Breathing { get; set; } = "0";               // Field 6
    public string Home { get; set; } = "Town";                 // Field 7
    public string Datable { get; set; } = "datable";           // Field 8
    public string Relationship { get; set; } = "null";         // Field 9
    public string SpawnMap { get; set; } = "Town";             // Field 10
    public int SpawnX { get; set; } = 1;
    public int SpawnY { get; set; } = 1;
    public string Loves { get; set; } = "(74)";                // Field 12
    public string Likes { get; set; } = "(206)";               // Field 13
    public string Dislikes { get; set; } = "(168)";            // Field 14
    public string Hates { get; set; } = "(172)";               // Field 15

    // Build the full disposition string
    public string Build(string displayName)
    {
        // Extract gender value
        var genderVal = Gender.Split(' ')[0];
        return $"{Age}/{Manner}/{Personality}/{Optimism}/{genderVal}/{Breathing}/{Home}/datable/null/{SpawnMap} {SpawnX} {SpawnY}/{displayName}/loves {Loves}/likes {Likes}/dislikes {Dislikes}/hates {Hates}";
    }
}

public class NpcGeneratorService
{
    public string Generate(NpcInputData data, List<KeyValuePair<string,string>>? dialogues = null, List<KeyValuePair<string,string>>? schedules = null, NpcDisposition? disposition = null, List<KeyValuePair<string,string>>? events = null)
    {
        string modName = $"[CP] {data.NpcDisplayName}";
        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), modName);
        string assetsDir = Path.Combine(outputDir, "assets");

        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(assetsDir);

        string portraitTargetName = $"{data.NpcInternalName}_Portraits.png";
        string spriteTargetName = $"{data.NpcInternalName}_Sprite.png";
        File.Copy(data.SourcePortraitPath, Path.Combine(assetsDir, portraitTargetName), true);
        File.Copy(data.SourceSpritePath, Path.Combine(assetsDir, spriteTargetName), true);

        var manifest = new Manifest
        {
            Name = data.NpcDisplayName,
            Author = data.Author,
            Description = $"Adds {data.NpcDisplayName} to Stardew Valley.",
            UniqueID = $"{data.Author}.{data.NpcInternalName}"
        };

        var contentPack = new ContentPack();
        
        contentPack.Changes.Add(new ContentChange 
        { 
            Action = "Load", 
            Target = $"Portraits/{data.NpcInternalName}", 
            FromFile = $"assets/{portraitTargetName}" 
        });
        
        contentPack.Changes.Add(new ContentChange 
        { 
            Action = "Load", 
            Target = $"Characters/{data.NpcInternalName}", 
            FromFile = $"assets/{spriteTargetName}" 
        });
        
        contentPack.Changes.Add(new ContentChange
        {
            Action = "EditData",
            Target = "Data/NPCDispositions",
            Entries = new Dictionary<string, string>
            {
                [data.NpcInternalName] = disposition?.Build(data.NpcDisplayName) ?? $"adult/neutral/neutral/0/0/0/Town/datable/null/Town 1 1/{data.NpcDisplayName}/loves (74)/likes (206)/dislikes (168)/hates (172)"
            }
        });
        
        // Dialogues: use provided dialogues or fall back to defaults
        var dialogueEntries = new Dictionary<string, string>();
        if (dialogues != null && dialogues.Count > 0)
        {
            foreach (var kv in dialogues)
                dialogueEntries[kv.Key] = kv.Value;
        }
        else
        {
            dialogueEntries["Introduction"] = $"Hello @, I am {data.NpcDisplayName}. Nice to meet you!";
            dialogueEntries["Mon"] = "Monday... I feel lazy today.";
            dialogueEntries["spring"] = "Spring weather is beautiful.";
        }

        contentPack.Changes.Add(new ContentChange
        {
            Action = "EditData",
            Target = $"Characters/Dialogue/{data.NpcInternalName}",
            Entries = dialogueEntries
        });
        
        // Schedules: use provided schedules or defaults
        var scheduleEntries = new Dictionary<string, string>();
        if (schedules != null && schedules.Count > 0)
        {
            foreach (var kv in schedules)
            {
                // kv.Value format: timeRange|location|x|y|z|direction  (timeRange could be single or start-end)
                var season = kv.Key;
                var parts = kv.Value.Split('|');
                if (parts.Length >= 5)
                {
                    var timeRange = parts[0];
                    var location = parts[1];
                    var x = parts[2];
                    var y = parts[3];
                    var z = parts[4];
                    var direction = parts.Length > 5 ? parts[5] : "0";
                    // convert timeRange e.g. 800-1200 into a single segment; for now create one segment
                    string segment;
                    if (timeRange.Contains('-'))
                    {
                        var start = timeRange.Split('-')[0];
                        segment = $"{start} {location} {x} {y} {z} {direction}";
                    }
                    else
                    {
                        segment = $"{timeRange} {location} {x} {y} {z} {direction}";
                    }
                    scheduleEntries[season] = segment;
                }
            }
        }
        else
        {
            scheduleEntries["spring"] = "800 Town 10 10 2 0/1200 Saloon 5 20 0 1/1800 Town 1 1 3 2";
            scheduleEntries["rain"] = "900 Town 1 1 0 0";
        }

        contentPack.Changes.Add(new ContentChange
        {
            Action = "EditData",
            Target = $"Characters/schedules/{data.NpcInternalName}",
            Entries = scheduleEntries
        });

        // Events: use provided events list or fallback to a simple default
        var eventEntries = new Dictionary<string, string>();
        if (events != null && events.Count > 0)
        {
            foreach (var kv in events)
            {
                // kv.Key is the event key, kv.Value is the event value string
                eventEntries[kv.Key] = kv.Value;
            }
        }
        else
        {
            eventEntries[$"99990001/f {data.NpcInternalName} 500"] = $"spring/64 17/{data.NpcInternalName} 64 19 2 farmer 64 25 0/skippable/emote {data.NpcInternalName} 8/speak {data.NpcInternalName} \"Oh @ what are you doing here?/end";
        }

        contentPack.Changes.Add(new ContentChange
        {
            Action = "EditData",
            Target = "Data/Events/Town",
            Entries = eventEntries
        });

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        File.WriteAllText(Path.Combine(outputDir, "manifest.json"), JsonSerializer.Serialize(manifest, options));
        File.WriteAllText(Path.Combine(outputDir, "content.json"), JsonSerializer.Serialize(contentPack, options));

        return outputDir;
    }
}

public class Manifest
{
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string Description { get; set; } = "";
    public string MinimumApiVersion { get; set; } = "3.18.0";
    public string UniqueID { get; set; } = "";
    public ContentPackFor ContentPackFor { get; set; } = new ContentPackFor();
}

public class ContentPackFor
{
    public string UniqueID { get; set; } = "Pathoschild.ContentPatcher";
}

public class ContentPack
{
    public string Format { get; set; } = "2.8.1";
    public List<ContentChange> Changes { get; set; } = new List<ContentChange>();
}

public class ContentChange
{
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public string? FromFile { get; set; }
    public Dictionary<string, string>? Entries { get; set; }
}