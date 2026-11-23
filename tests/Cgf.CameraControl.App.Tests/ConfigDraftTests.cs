using System.Reactive.Linq;
using Cgf.CameraControl.App.Configuration;
using Cgf.CameraControl.App.Editing;
using Cgf.CameraControl.App.Hosting;
using Cgf.CameraControl.App.Localization;
using Cgf.CameraControl.Atem.VideoMixer.Blackmagicdesign;
using Cgf.CameraControl.Cameras.ViscaOverIp.Camera;
using Cgf.CameraControl.Core.Configuration;
using Cgf.CameraControl.Core.VideoMixer;
using Cgf.CameraControl.Core.VideoMixer.Passthrough;
using Cgf.CameraControl.Input.Sdl.Hmi.Gamepad.Shared;

namespace Cgf.CameraControl.App.Tests;

public class ConfigDraftTests
{
    private const string Desk =
        """
        {
          "cams": [
            { "type": "viscaoverip", "instance": 1, "ip": "10.0.0.1" },
            { "type": "viscaoverip", "instance": 2, "ip": "10.0.0.2" }
          ],
          "videoMixers": [ { "type": "passthrough/default", "instance": 1 } ],
          "interfaces": [
            {
              "type": "gamepad", "instance": 1, "videoMixer": 1,
              "connectionChange": { "type": "direct", "default": { "up": 1 } },
              "cameraMap": { "1": 1, "2": 2 },
              "functions": { "wide": { "type": "key", "index": 1 } },
              "pad": { "default": { "up": "wide" } }
            }
          ]
        }
        """;

    public class WhatItReads : ConfigDraftTests
    {
        [Fact]
        public void EveryCollectionIsRead()
        {
            var draft = Draft(Desk);

            Assert.Equal(2, draft.Cameras.Count);
            Assert.Single(draft.Mixers);
            Assert.Single(draft.Interfaces);
        }

        /// What no field covers, the functions and the bindings among them, is still in the record to
        /// be written back.
        [Fact]
        public void ReadingAnEntryDoesNotChangeIt()
        {
            var draft = Draft(Desk);

            var written = Load(Write(draft.ToConfig()), out var issues);

            Assert.Empty(issues);
            Assert.False(draft.IsDirty);
            Assert.Equal("wide", Assert.IsType<GamepadConfiguration>(written.Interfaces[0]).Pad!.Default![ButtonDirection.Up]);
            Assert.Equal(Write(Load(Desk, out _)), Write(draft.ToConfig()));
        }

        [Fact]
        public void AFieldSurvivesToTheFileItIsWrittenTo()
        {
            var draft = Draft(Desk);
            Address(draft.Cameras[0]).Value = "10.0.0.9";

            var written = Load(Write(draft.ToConfig()), out _);

            Assert.True(draft.IsDirty);
            Assert.Equal("10.0.0.9", Assert.IsType<ViscaOverIpCameraConfiguration>(written.Cams[0]).Ip);
        }

        /// The draft edits a copy, so the configuration it was opened from, and one it handed out
        /// earlier, stay as they were.
        [Fact]
        public void AnEditReachesNeitherTheLoadedNorAnEarlierConfiguration()
        {
            var loaded = Load(Desk, out _);
            var draft = ConfigDraft.From(new Localizer(), loaded);
            var earlier = draft.ToConfig();

            Address(draft.Cameras[0]).Value = "10.0.0.9";

            Assert.Equal("10.0.0.1", Assert.IsType<ViscaOverIpCameraConfiguration>(loaded.Cams[0]).Ip);
            Assert.Equal("10.0.0.1", Assert.IsType<ViscaOverIpCameraConfiguration>(earlier.Cams[0]).Ip);
            Assert.Equal("10.0.0.9", Assert.IsType<ViscaOverIpCameraConfiguration>(draft.ToConfig().Cams[0]).Ip);
        }
    }

    public class MixerTallyPolicy : ConfigDraftTests
    {
        [Theory]
        [InlineData("blackmagicdesign/atem", "preview-program", MixerTallyMode.PreviewProgram)]
        [InlineData("blackmagicdesign/atem", "program", MixerTallyMode.Program)]
        [InlineData("blackmagicdesign/atem", "none", MixerTallyMode.None)]
        [InlineData("passthrough/default", "preview-program", MixerTallyMode.PreviewProgram)]
        [InlineData("passthrough/default", "program", MixerTallyMode.Program)]
        [InlineData("passthrough/default", "none", MixerTallyMode.None)]
        public void StoredChoicesSelectTheirOptionWithoutChangingTheDraft(string type, string policy, MixerTallyMode expected)
        {
            var draft = Draft(Mixer(type, $$""", "tallyMode": "{{policy}}" """));
            var field = Assert.Single(draft.Mixers[0].Fields.OfType<ChoiceField>());
            Assert.Equal(policy, field.Value);
            Assert.NotNull(field.SelectedOption);
            Assert.Equal(policy, field.SelectedOption.Value);
            Assert.True(draft.CanSave);
            Assert.False(draft.IsDirty);
            Assert.Equal<MixerTallyMode?>(expected, TallyMode(draft.ToConfig().VideoMixers[0]));
        }

        /// The desk refuses a policy it does not know, in any other case too, so the editor shows the
        /// entry as the desk would read it.
        [Theory]
        [InlineData("blackmagicdesign/atem", "\"PROGRAM\"")]
        [InlineData("blackmagicdesign/atem", "\"invalid\"")]
        [InlineData("passthrough/default", "42")]
        public void AnUnknownStoredChoiceLeavesTheEntryUnreadable(string type, string value)
        {
            var draft = Draft(Mixer(type, $$""", "tallyMode": {{value}} """));

            Assert.Empty(draft.Mixers[0].Fields);
            Assert.Contains($"{type}[1].tallyMode", draft.Mixers[0].Problem, StringComparison.Ordinal);
            Assert.True(draft.CanSave);
        }

        [Fact]
        public void CameraProtocolNamesAndSelectionsRemainUnchanged()
        {
            var draft = Draft(Desk);
            var field = Assert.Single(draft.Cameras[0].Fields.OfType<ChoiceField>());
            Assert.Equal(field.Options, field.Items.Select(option => option.Caption.FirstAsync().Wait()));

            field.SelectedOption = field.Items.Single(option => option.Value == "avonic");

            Assert.Equal("avonic", field.Value);
            Assert.Equal(ViscaTallyMode.Avonic, Assert.IsType<ViscaOverIpCameraConfiguration>(draft.ToConfig().Cams[0]).TallyMode);
        }

        [Theory]
        [InlineData("blackmagicdesign/atem")]
        [InlineData("passthrough/default")]
        public void FriendlyNamesKeepTheStoredPolicyValues(string type)
        {
            var draft = Draft(Mixer(type));
            var field = Assert.Single(draft.Mixers[0].Fields.OfType<ChoiceField>());
            Assert.Equal(new[] { "Program and preview", "Program only", "Disabled" },
                field.Items.Select(option => option.Caption.FirstAsync().Wait()));
            Assert.NotNull(field.SelectedOption);
            Assert.Equal("preview-program", field.SelectedOption.Value);
            Assert.False(draft.IsDirty);

            field.SelectedOption = field.Items[^1];
            foreach (var option in field.Items)
            {
                field.SelectedOption = option;
                Assert.Equal(option.Value, field.Value);
                Assert.Equal<MixerTallyMode?>(Policies[option.Value], TallyMode(draft.ToConfig().VideoMixers[0]));
            }
        }

        [Theory]
        [InlineData("blackmagicdesign/atem")]
        [InlineData("passthrough/default")]
        public void BothMixerEditorsDefaultWithoutRewritingExistingConfigurations(string type)
        {
            var draft = Draft(Mixer(type));
            var field = Assert.Single(draft.Mixers[0].Fields.OfType<ChoiceField>());

            Assert.Equal("preview-program", field.Value);
            Assert.Equal(new[] { "preview-program", "program", "none" }, field.Options);
            Assert.False(draft.IsDirty);
            Assert.DoesNotContain("tallyMode", Write(draft.ToConfig()), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("blackmagicdesign/atem", "preview-program")]
        [InlineData("blackmagicdesign/atem", "program")]
        [InlineData("blackmagicdesign/atem", "none")]
        [InlineData("passthrough/default", "preview-program")]
        [InlineData("passthrough/default", "program")]
        [InlineData("passthrough/default", "none")]
        public void PolicySurvivesSavingWithoutChangingTheCameraProtocol(string type, string policy)
        {
            var draft = Draft(Mixer(type).Replace(
                "\"videoMixers\"",
                "\"cams\":[{\"type\":\"viscaoverip\",\"instance\":1,\"ip\":\"10.0.0.2\",\"tallyMode\":\"avonic\"}], \"videoMixers\"",
                StringComparison.Ordinal));
            var field = Assert.Single(draft.Mixers[0].Fields.OfType<ChoiceField>());
            field.Value = "none";
            field.Value = policy;

            var reopened = Draft(Write(draft.ToConfig()));
            Assert.True(draft.IsDirty);
            Assert.True(reopened.CanSave);
            Assert.Equal(policy, Assert.Single(reopened.Mixers[0].Fields.OfType<ChoiceField>()).Value);
            Assert.Equal("avonic", Assert.Single(reopened.Cameras[0].Fields.OfType<ChoiceField>()).Value);
        }

        private static Dictionary<string, MixerTallyMode> Policies { get; } = new()
        {
            ["preview-program"] = MixerTallyMode.PreviewProgram,
            ["program"] = MixerTallyMode.Program,
            ["none"] = MixerTallyMode.None,
        };

        private static MixerTallyMode? TallyMode(VideoMixerConfiguration mixer) => mixer switch
        {
            AtemConfiguration atem => atem.TallyMode,
            PassthroughConfiguration passthrough => passthrough.TallyMode,
            _ => throw new ArgumentOutOfRangeException(nameof(mixer)),
        };
    }

    public class PanTiltSpeed : ConfigDraftTests
    {
        [Theory]
        [InlineData("viscaoverip")]
        [InlineData("Websocket.PtzLanc")]
        [InlineData("Signalr.PtzLanc")]
        public void EveryCameraOffersTheSettingWithoutChangingAnAbsentValue(string type)
        {
            var draft = Draft(Desk);
            var camera = draft.Add(EntryKind.Camera, type);
            var field = Assert.Single(camera.Fields.OfType<PercentField>());

            Assert.Equal("100", field.Value);
            Assert.Equal(100, field.SliderValue);
            Assert.DoesNotContain("panTiltSpeedPercent", Write(draft.ToConfig()), StringComparison.Ordinal);
            Assert.DoesNotContain(draft.Interfaces[0].Fields, entry => entry is PercentField);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(50)]
        [InlineData(100)]
        public void SliderAndTextStayLinkedAndSurviveSaving(int percent)
        {
            var draft = Draft(Desk);
            var field = Assert.Single(draft.Cameras[0].Fields.OfType<PercentField>());
            field.SliderValue = 75;
            field.SliderValue = percent;

            Assert.Equal(percent.ToString(), field.Value);
            Assert.True(draft.IsDirty);
            Assert.True(draft.CanSave);
            var reopened = Draft(Write(draft.ToConfig()));
            var read = Assert.Single(reopened.Cameras[0].Fields.OfType<PercentField>());
            Assert.Equal(percent, read.SliderValue);
            Assert.False(reopened.IsDirty);

            read.Value = "25";
            Assert.Equal(25, read.SliderValue);
            Assert.True(reopened.CanSave);
        }

        [Theory]
        [InlineData("")]
        [InlineData("no")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("101")]
        [InlineData("50.5")]
        public void InvalidTextBlocksSavingAndCanBeCorrectedWithTheSlider(string value)
        {
            var draft = Draft(Desk);
            var field = Assert.Single(draft.Cameras[0].Fields.OfType<PercentField>());
            field.Value = value;

            Assert.NotNull(field.Error);
            Assert.False(draft.CanSave);
            field.SliderValue = 50;
            Assert.Null(field.Error);
            Assert.Equal("50", field.Value);
            Assert.True(draft.CanSave);
        }
    }

    public class WhatStopsASave : ConfigDraftTests
    {
        [Theory]
        [InlineData("tallyMode")]
        [InlineData("backend")]
        public void AChoiceThatIsNotAnOptionBlocksTheSaveUntilCorrected(string key)
        {
            var draft = Draft(Desk);
            EntryDraft entry = key == "backend" ? draft.Interfaces[0] : draft.Cameras[0];
            var field = Assert.Single(entry.Fields.OfType<ChoiceField>());

            field.Value = "unknown";

            Assert.False(draft.CanSave);
            Assert.NotNull(field.Error);
            Assert.Null(field.SelectedOption);

            field.SelectedOption = field.Items.First();

            Assert.True(draft.CanSave);
            Assert.Null(field.Error);
        }

        [Fact]
        public void AConfigurationThatAddsUpCanBeSaved()
        {
            Assert.True(Draft(Desk).CanSave);
            Assert.Empty(Draft(Desk).Issues);
        }

        [Fact]
        public void ARequiredFieldLeftEmpty()
        {
            var draft = Draft(Desk);

            Address(draft.Cameras[0]).Value = string.Empty;

            Assert.False(draft.CanSave);
            Assert.NotNull(draft.Cameras[0].Problem);
        }

        [Fact]
        public void TwoEntriesClaimingOneInstance()
        {
            var draft = Draft(Desk);

            draft.Cameras[1].InstanceField!.Value = "1";

            Assert.False(draft.CanSave);
            Assert.All(draft.Cameras, camera => Assert.NotNull(camera.Problem));
        }

        [Fact]
        public void AMapNamingACameraThatIsNotConfigured()
        {
            var draft = Draft(Desk);

            draft.Remove(draft.Cameras[1]);
            Map(draft.Interfaces[0]).Rows[0].Value = "7";

            Assert.False(draft.CanSave);
        }

        [Fact]
        public void AnInterfaceNamingAMixerThatIsNotConfigured()
        {
            var draft = Draft(Desk);

            Mixer(draft.Interfaces[0]).Value = "4";

            Assert.False(draft.CanSave);
            Assert.NotNull(draft.Interfaces[0].Problem);
        }

        [Fact]
        public void ABindingNamingAFunctionThatIsNotDefined()
        {
            var draft = Draft(Desk.Replace("\"wide\": { \"type\": \"key\", \"index\": 1 }", string.Empty));

            Assert.False(draft.CanSave);
        }

        /// A mixer input with no camera on it is an ordinary desk, not a mistake, so it must not be
        /// what stops the file being written.
        [Fact]
        public void ADirectionSelectingAnInputWithNoCameraDoesNot()
        {
            var draft = Draft(Desk);

            Map(draft.Interfaces[0]).RemoveRowCommand.Execute(Map(draft.Interfaces[0]).Rows[0]);

            Assert.True(draft.CanSave);
        }
    }

    /// An entry the loader could not read cannot be edited on the form, so it is shown with what is
    /// wrong with it and written back as it was read, rather than lost or holding up the rest.
    public class AnEntryTheLoaderCouldNotRead : ConfigDraftTests
    {
        [Theory]
        [InlineData("\"ip\": \"10.0.0.2\", \"port\": \"52381\"")]
        [InlineData("\"ip\": 10")]
        [InlineData("\"ip\": \"10.0.0.2\", \"panTiltInvert\": \"true\"")]
        [InlineData("\"ip\": \"10.0.0.2\", \"panTiltSpeedPercent\": \"50\"")]
        [InlineData("\"ip\": \"10.0.0.2\", \"tallyMode\": \"unknown\"")]
        [InlineData("\"ip\": \"10.0.0.2\", \"lens\": \"wide\"")]
        public void IsShownWithItsProblemAndNoFields(string properties)
        {
            var draft = Draft(Desk.Replace("\"ip\": \"10.0.0.2\"", properties));

            var camera = draft.Cameras[1];
            Assert.Equal(2, draft.Cameras.Count);
            Assert.Empty(camera.Fields);
            Assert.Null(camera.InstanceField);
            Assert.StartsWith("cams[1]: viscaoverip[2]", camera.Problem, StringComparison.Ordinal);
            Assert.Equal(camera.Unreadable, camera.Problem);
            Assert.Equal("viscaoverip [2]", camera.Title);
        }

        [Fact]
        public void DoesNotStopTheRestBeingSaved()
        {
            var draft = Draft(Desk.Replace("\"ip\": \"10.0.0.2\"", "\"ip\": \"10.0.0.2\", \"port\": \"camera\""));

            Assert.True(draft.CanSave);
            Assert.Empty(draft.Issues);
            Assert.False(draft.IsDirty);
        }

        [Fact]
        public void IsWrittenBackAsItWasRead()
        {
            var json = Desk.Replace("\"ip\": \"10.0.0.2\"", "\"ip\": \"10.0.0.2\", \"port\": \"52381\"");
            var draft = Draft(json);
            Address(draft.Cameras[0]).Value = "10.0.0.9";

            var written = Load(Write(draft.ToConfig()), out var issues);

            Assert.Single(issues);
            Assert.Equal(
                """{"type":"viscaoverip","instance":2,"ip":"10.0.0.2","port":"52381"}""",
                Assert.IsType<InvalidCameraConfiguration>(written.Cams[1]).Json);
        }

        /// It keeps the number it was written with, so what refers to it is not reported as missing.
        [Fact]
        public void KeepsItsInstance()
        {
            var draft = Draft(Desk.Replace("\"ip\": \"10.0.0.2\"", "\"ip\": 10"));

            Assert.Equal(2, draft.Cameras[1].Instance);
            Assert.Equal(3, draft.NextInstance(EntryKind.Camera));
            Assert.Single(draft.ReferencesTo(draft.Cameras[1]));
        }

        [Fact]
        public void AnInterfaceOfAKindNothingClaimsIsKept()
        {
            var draft = Draft(Desk.Replace("\"type\": \"gamepad\"", "\"type\": \"joystick\""));

            var entry = Assert.Single(draft.Interfaces);
            Assert.Contains("joystick", entry.Problem, StringComparison.Ordinal);
            Assert.True(draft.CanSave);
            Assert.Contains("\"type\": \"joystick\"", Write(draft.ToConfig()), StringComparison.Ordinal);
        }
    }

    public class WhatTheLoaderWouldReject : ConfigDraftTests
    {
        [Fact]
        public void APortAboveTheLastOneBlocksTheSave()
        {
            var draft = Draft(Desk);

            draft.Cameras[1].Fields.OfType<NumberField>().Single(field => field.Key == "port").Value = "70000";

            Assert.False(draft.CanSave);
        }

        [Fact]
        public void AnEmptyPortIsTheProtocolsOwn()
        {
            var draft = Draft(Desk.Replace("\"ip\": \"10.0.0.2\"", "\"ip\": \"10.0.0.2\", \"port\": 1259"));
            var port = draft.Cameras[1].Fields.OfType<NumberField>().Single(field => field.Key == "port");
            Assert.Equal("1259", port.Value);

            port.Value = string.Empty;

            Assert.True(draft.CanSave);
            Assert.Null(Assert.IsType<ViscaOverIpCameraConfiguration>(draft.ToConfig().Cams[1]).Port);
            Assert.DoesNotContain("port", Write(draft.ToConfig()), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("controller.local", false)]
        [InlineData("ftp://controller.local", false)]
        [InlineData("http://controller.local:5000/hub", true)]
        [InlineData("http://controller.local:5000/hub?token=1", false)]
        [InlineData("http://controller.local:5000/hub#main", false)]
        public void AConnectionUrlHasToBeAnHttpAddress(string url, bool saves)
        {
            var draft = Draft("""
                {"cams":[{"type":"Signalr.PtzLanc","instance":1,"connectionUrl":"http://controller.local:5000","connectionPort":"COM6"}]}
                """);

            draft.Cameras[0].Fields.OfType<TextField>().Single(field => field.Key == "connectionUrl").Value = url;

            Assert.Equal(saves, draft.CanSave);
        }

        [Fact]
        public void AnHidPadNeedsASerialNumber()
        {
            var draft = Draft(Desk);
            var backend = draft.Interfaces[0].Fields.OfType<ChoiceField>().Single(field => field.Key == "backend");

            backend.Value = "hid";

            Assert.False(draft.CanSave);
            Assert.NotNull(draft.Interfaces[0].Fields.OfType<TextField>().Single(field => field.Key == "serialNumber").Error);
        }

        [Fact]
        public void AnHidPadWithASerialNumberCanBeSaved()
        {
            var draft = Draft(Desk);
            draft.Interfaces[0].Fields.OfType<TextField>().Single(field => field.Key == "serialNumber").Value = "ABC123";

            draft.Interfaces[0].Fields.OfType<ChoiceField>().Single(field => field.Key == "backend").Value = "hid";

            Assert.True(draft.CanSave);
            var pad = Assert.IsType<GamepadConfiguration>(draft.ToConfig().Interfaces[0]);
            Assert.Equal(GamepadBackend.Hid, pad.Backend);
            Assert.Equal("ABC123", pad.SerialNumber);
        }

        [Fact]
        public void AnHidSerialWithoutALetterOrDigitBlocksTheSave()
        {
            var draft = Draft(Desk);
            draft.Interfaces[0].Fields.OfType<TextField>().Single(field => field.Key == "serialNumber").Value = "---";

            draft.Interfaces[0].Fields.OfType<ChoiceField>().Single(field => field.Key == "backend").Value = "hid";

            Assert.False(draft.CanSave);
        }

        [Theory]
        [InlineData("logitech/F710")]
        [InlineData("logitech/Rumblepad2")]
        public void OnlyTheF310OffersTheHidBackend(string type)
        {
            var draft = Draft(Desk.Replace("\"type\": \"gamepad\"", $"\"type\": \"{type}\""));

            var backend = draft.Interfaces[0].Fields.OfType<ChoiceField>().Single(field => field.Key == "backend");
            Assert.Equal(["sdl"], backend.Options);
            backend.Value = "hid";
            Assert.False(draft.CanSave);
        }

        [Fact]
        public void ADeadzoneThatIsNotFiniteBlocksTheSave()
        {
            var draft = Draft(Desk);

            draft.Interfaces[0].Fields.OfType<FractionField>().Single(field => field.Key == "deadzone").Value = double.NaN.ToString(System.Globalization.CultureInfo.CurrentUICulture);

            Assert.False(draft.CanSave);
        }
    }

    public class WhatTheRecordsRulesRefuse : ConfigDraftTests
    {
        [Fact]
        public void ARefusedValueIsShownOnTheFieldThatWroteIt()
        {
            var draft = Draft(Desk);
            var port = draft.Cameras[1].Fields.OfType<NumberField>().Single(field => field.Key == "port");

            port.Value = "70000";

            Assert.Equal("The field port must be between 1 and 65535.", port.Error);
            Assert.Equal(["viscaoverip [2]: The field port must be between 1 and 65535."], draft.Issues);
        }

        [Fact]
        public void TheInstanceIsHeldToTheSameRuleAsEveryOtherProperty()
        {
            var draft = Draft(Desk);
            var instance = Assert.IsType<NumberField>(draft.Cameras[0].InstanceField);

            instance.Value = "0";

            Assert.Equal("The field instance must be between 1 and 2147483647.", instance.Error);
            Assert.False(draft.CanSave);
        }

        [Fact]
        public void AnEmptyRequiredTextIsRefusedByTheRecord()
        {
            var draft = Draft(Desk);

            Address(draft.Cameras[0]).Value = string.Empty;

            Assert.Equal("The ip field is required.", Address(draft.Cameras[0]).Error);
        }

        [Fact]
        public void TextThatIsNotANumberStaysTheFieldsOwnError()
        {
            var draft = Draft(Desk);
            var port = draft.Cameras[1].Fields.OfType<NumberField>().Single(field => field.Key == "port");

            port.Value = "many";

            Assert.Equal(new Localizer().Text("edit.notANumber"), port.Error);
            Assert.False(draft.CanSave);
        }

        [Fact]
        public void ADeadzoneOverHalfTheTravelIsRefused()
        {
            var draft = Draft(Desk);
            var deadzone = draft.Interfaces[0].Fields.OfType<FractionField>().Single(field => field.Key == "deadzone");

            deadzone.Value = 0.7.ToString(System.Globalization.CultureInfo.CurrentUICulture);

            Assert.Equal("The field deadzone must be between 0 and 0.5.", deadzone.Error);
            Assert.False(draft.CanSave);
        }

        [Fact]
        public void TheSerialNumberIsRefusedOnceTheBackendNeedsIt()
        {
            var draft = Draft(Desk);
            var serial = draft.Interfaces[0].Fields.OfType<TextField>().Single(field => field.Key == "serialNumber");
            Assert.Null(serial.Error);

            draft.Interfaces[0].Fields.OfType<ChoiceField>().Single(field => field.Key == "backend").Value = "hid";

            Assert.Equal("The HID backend requires a serial number.", serial.Error);
        }

        [Fact]
        public void AValueNoFieldShowsIsListedWithItsPath()
        {
            var pad = Assert.IsType<GamepadConfiguration>(Load(Desk, out _).Interfaces[0]);
            pad.Functions = new Dictionary<string, Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange.SpecialFunctionConfiguration>
            {
                ["wide"] = new Input.Sdl.Hmi.Gamepad.Shared.ConnectionChange.MacroLoopSpecialFunctionConfiguration { Indexes = [] },
            };

            var draft = ConfigDraft.From(new Localizer(), new RootConfig([], [], [pad]));

            Assert.Contains(
                "gamepad [1]: functions.wide.indexes: The field indexes must hold at least one entry.",
                draft.Issues);
            Assert.False(draft.CanSave);
        }
    }

    public class AddingAndRemoving : ConfigDraftTests
    {
        [Fact]
        public void ANewEntryTakesTheLowestFreeInstance()
        {
            var draft = Draft(Desk);

            Assert.Equal(3, draft.Add(EntryKind.Camera, "viscaoverip").Instance);
        }

        /// A new interface without them is rejected by the loader whole, so it is born holding them
        /// rather than being written into a file that will not open.
        [Theory]
        [InlineData("gamepad")]
        [InlineData("keyboard")]
        public void ANewInterfaceCarriesWhatTheLoaderRequires(string type)
        {
            var draft = Draft(Desk);
            var added = draft.Add(EntryKind.Interface, type);

            var written = Load(Write(draft.ToConfig()), out var issues);

            Assert.Empty(issues);
            Assert.True(draft.CanSave);
            Assert.Equal(2, added.Instance);
            Assert.Equal(type, written.Interfaces[1].Type);
            Assert.Equal(1, Assert.IsAssignableFrom<ControlInterfaceConfiguration>(written.Interfaces[1]).VideoMixer);
        }

        [Fact]
        public void DeletingACameraTakesItOutOfTheMapsThatNamedIt()
        {
            var draft = Draft(Desk);

            draft.Remove(draft.Cameras[0]);

            Assert.DoesNotContain(1, Map(draft.Interfaces[0]).Targets);
            Assert.DoesNotContain(1, Assert.IsType<GamepadConfiguration>(draft.ToConfig().Interfaces[0]).CameraMap.Keys);
            Assert.True(draft.CanSave);
        }

        /// A blank row is invalid, so the one thing that is always known is filled in: the lowest
        /// input nothing has taken, and the camera of the same number, which is how an unrearranged
        /// desk is wired.
        [Fact]
        public void ANewMapRowProposesItsNumbers()
        {
            var draft = Draft(Desk);
            var map = Map(draft.Interfaces[0]);

            map.AddRowCommand.Execute(null);

            Assert.Equal("3", map.Rows[^1].Key);
            Assert.Equal("3", map.Rows[^1].Value);
            Assert.Equal(3, Assert.IsType<GamepadConfiguration>(draft.ToConfig().Interfaces[0]).CameraMap[3]);
        }

        /// A pad is picked from the ones plugged in, and a desk built away from them still takes a
        /// serial nobody can see right now.
        [Fact]
        public void APadIsOfferedTheSerialsThatAreConnected()
        {
            var draft = ConfigDraft.From(new Localizer(), Load(Desk, out _), ["83234F94"]);

            var serial = draft.Interfaces[0].Fields.OfType<TextField>()
                .First(entry => entry.Key == "serialNumber");

            Assert.True(serial.HasSuggestions);
            Assert.Equal("83234F94", Assert.Single(serial.Suggestions));
        }

        [Fact]
        public void WhatStillNamesAnEntryIsReportedBeforeItGoes()
        {
            var draft = Draft(Desk);

            Assert.Single(draft.ReferencesTo(draft.Cameras[0]));
            Assert.Single(draft.ReferencesTo(draft.Mixers[0]));
        }
    }

    private static RootConfig Load(string json, out IReadOnlyList<string> issues) =>
        ConfigLoader.Load(json, out issues);

    private static string Write(RootConfig config) => ConfigWriter.Write(config);

    private static ConfigDraft Draft(string json) => ConfigDraft.From(new Localizer(), Load(json, out _));

    /// A desk holding one mixer of the type, with what that type requires.
    private static string Mixer(string type, string extra = "") =>
        type == AtemConfiguration.TypeName
            ? $$"""{"videoMixers":[{"type":"{{type}}","instance":1,"ip":"10.0.0.1","mixEffectBlock":0{{extra}}}]}"""
            : $$"""{"videoMixers":[{"type":"{{type}}","instance":1{{extra}}}]}""";

    private static TextField Address(EntryDraft entry) =>
        entry.Fields.OfType<TextField>().First(field => field.Key == "ip");

    private static NumberField Mixer(EntryDraft entry) =>
        entry.Fields.OfType<NumberField>().First(field => field.Key == "videoMixer");

    private static MapField Map(EntryDraft entry) => entry.Fields.OfType<MapField>().First();
}
