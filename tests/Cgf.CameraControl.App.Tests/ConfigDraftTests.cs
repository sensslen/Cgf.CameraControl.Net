using System.Reactive.Linq;
using Cgf.CameraControl.App.Editing;
using Cgf.CameraControl.Core.Configuration;

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

        /// The point of editing the JSON rather than a deserialized shape: what no field covers, the
        /// functions and the bindings among them, is still there to be written back.
        [Fact]
        public void ReadingAnEntryDoesNotChangeIt()
        {
            var draft = Draft(Desk);

            var written = ConfigLoader.Load(ConfigWriter.Write(draft.ToConfig()), out var issues);

            Assert.Empty(issues);
            Assert.False(draft.IsDirty);
            Assert.Equal("wide", written.Interfaces[0].Raw.GetProperty("pad").GetProperty("default")
                .GetProperty("up").GetString());
        }

        [Fact]
        public void AFieldSurvivesToTheFileItIsWrittenTo()
        {
            var draft = Draft(Desk);
            Address(draft.Cameras[0]).Value = "10.0.0.9";

            var written = ConfigLoader.Load(ConfigWriter.Write(draft.ToConfig()), out _);

            Assert.True(draft.IsDirty);
            Assert.Equal("10.0.0.9", written.Cams[0].Raw.GetProperty("ip").GetString());
        }
    }

    public class MixerTallyPolicy : ConfigDraftTests
    {
        [Theory]
        [InlineData("blackmagicdesign/atem")]
        [InlineData("passthrough/default")]
        public void InvalidStoredChoicesBlockSavingUntilCorrected(string type)
        {
            foreach (var value in new[] { "\"invalid\"", "\"\"", "42", "true", "null", "[]", "{}" })
            {
                var draft = Draft($$"""
                    {"videoMixers":[{"type":"{{type}}","instance":1,"ip":"10.0.0.1","mixEffectBlock":0,
                                    "tallyMode":{{value}}}]}
                    """);
                var field = Assert.Single(draft.Mixers[0].Fields.OfType<ChoiceField>());
                Assert.False(draft.IsDirty);
                Assert.False(draft.CanSave);
                Assert.NotNull(field.Error);
                Assert.Null(field.SelectedOption);
                Assert.NotNull(draft.Mixers[0].Problem);

                field.SelectedOption = field.Items.First();

                Assert.True(draft.CanSave);
                Assert.Null(field.Error);
                Assert.True(draft.IsDirty);
                Assert.Equal("preview-program",
                    draft.ToConfig().VideoMixers[0].Raw.GetProperty("tallyMode").GetString());
            }
        }

        [Theory]
        [InlineData("blackmagicdesign/atem", "preview-program")]
        [InlineData("blackmagicdesign/atem", "program")]
        [InlineData("blackmagicdesign/atem", "none")]
        [InlineData("passthrough/default", "preview-program")]
        [InlineData("passthrough/default", "program")]
        [InlineData("passthrough/default", "none")]
        public void MixedCaseChoicesSelectCanonicalOptionsWithoutRewritingOnRead(string type, string policy)
        {
            var stored = policy.ToUpperInvariant();
            var draft = Draft($$"""
                {"videoMixers":[{"type":"{{type}}","instance":1,"ip":"10.0.0.1","mixEffectBlock":0,
                                "tallyMode":"{{stored}}"}]}
                """);
            var field = Assert.Single(draft.Mixers[0].Fields.OfType<ChoiceField>());
            Assert.Equal(policy, field.Value);
            Assert.NotNull(field.SelectedOption);
            Assert.Equal(policy, field.SelectedOption.Value);
            Assert.True(draft.CanSave);
            Assert.False(draft.IsDirty);
            Assert.Equal(stored, draft.ToConfig().VideoMixers[0].Raw.GetProperty("tallyMode").GetString());
        }

        [Fact]
        public void CameraProtocolNamesAndSelectionsRemainUnchanged()
        {
            var draft = Draft(Desk);
            var field = Assert.Single(draft.Cameras[0].Fields.OfType<ChoiceField>());
            Assert.Equal(field.Options, field.Items.Select(option => option.Caption.FirstAsync().Wait()));

            field.SelectedOption = field.Items.Single(option => option.Value == "avonic");

            Assert.Equal("avonic", field.Value);
            Assert.Equal("avonic", draft.ToConfig().Cams[0].Raw.GetProperty("tallyMode").GetString());
        }

        [Theory]
        [InlineData("blackmagicdesign/atem")]
        [InlineData("passthrough/default")]
        public void FriendlyNamesKeepTheStoredPolicyValues(string type)
        {
            var draft = Draft($$"""
                {"videoMixers":[{"type":"{{type}}","instance":1,"ip":"10.0.0.1","mixEffectBlock":0}]}
                """);
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
                Assert.Equal(option.Value, draft.ToConfig().VideoMixers[0].Raw.GetProperty("tallyMode").GetString());
            }
        }

        [Theory]
        [InlineData("blackmagicdesign/atem")]
        [InlineData("passthrough/default")]
        public void BothMixerEditorsDefaultWithoutRewritingExistingConfigurations(string type)
        {
            var draft = Draft($$"""
                {"videoMixers":[{"type":"{{type}}","instance":1,"ip":"10.0.0.1","mixEffectBlock":0}]}
                """);
            var field = Assert.Single(draft.Mixers[0].Fields.OfType<ChoiceField>());

            Assert.Equal("preview-program", field.Value);
            Assert.Equal(new[] { "preview-program", "program", "none" }, field.Options);
            Assert.False(draft.IsDirty);
            Assert.False(draft.ToConfig().VideoMixers[0].Raw.TryGetProperty("tallyMode", out _));
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
            var draft = Draft($$"""
                {
                  "cams":[{"type":"viscaoverip","instance":1,"ip":"10.0.0.2","tallyMode":"avonic"}],
                  "videoMixers":[{"type":"{{type}}","instance":1,"ip":"10.0.0.1","mixEffectBlock":0}]
                }
                """);
            var field = Assert.Single(draft.Mixers[0].Fields.OfType<ChoiceField>());
            field.Value = "none";
            field.Value = policy;

            var reopened = Draft(ConfigWriter.Write(draft.ToConfig()));
            Assert.True(draft.IsDirty);
            Assert.True(reopened.CanSave);
            Assert.Equal(policy, Assert.Single(reopened.Mixers[0].Fields.OfType<ChoiceField>()).Value);
            Assert.Equal("avonic", Assert.Single(reopened.Cameras[0].Fields.OfType<ChoiceField>()).Value);
        }
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
            Assert.False(camera.ToEntry().Raw.TryGetProperty("panTiltSpeedPercent", out _));
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
            var reopened = Draft(ConfigWriter.Write(draft.ToConfig()));
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
        [InlineData("viscaoverip", "tallyMode")]
        [InlineData("gamepad", "backend")]
        public void UnknownCameraAndBackendChoicesAlsoRequireCorrection(string type, string key)
        {
            var draft = Draft(Desk.Replace(
                $"\"type\": \"{type}\", \"instance\": 1",
                $"\"type\": \"{type}\", \"instance\": 1, \"{key}\": \"unknown\""));
            var entry = type == "gamepad" ? draft.Interfaces[0] : draft.Cameras[0];
            var field = Assert.Single(entry.Fields.OfType<ChoiceField>());
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

            draft.Cameras[1].InstanceField.Value = "1";

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
        [Fact]
        public void ANewInterfaceCarriesWhatTheLoaderRequires()
        {
            var draft = Draft(Desk);

            var entry = draft.Add(EntryKind.Interface, "keyboard").ToEntry().Raw;

            Assert.True(entry.TryGetProperty("connectionChange", out _));
            Assert.True(entry.TryGetProperty("cameraMap", out _));
            Assert.True(entry.TryGetProperty("keys", out _));
        }

        [Fact]
        public void DeletingACameraTakesItOutOfTheMapsThatNamedIt()
        {
            var draft = Draft(Desk);

            draft.Remove(draft.Cameras[0]);

            Assert.DoesNotContain(1, Map(draft.Interfaces[0]).Targets);
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
        }

        /// A pad is picked from the ones plugged in, and a desk built away from them still takes a
        /// serial nobody can see right now.
        [Fact]
        public void APadIsOfferedTheSerialsThatAreConnected()
        {
            var draft = ConfigDraft.From(ConfigLoader.Load(Desk, out _), ["83234F94"]);

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

    private static ConfigDraft Draft(string json) => ConfigDraft.From(ConfigLoader.Load(json, out _));

    private static TextField Address(EntryDraft entry) =>
        entry.Fields.OfType<TextField>().First(field => field.Key == "ip");

    private static NumberField Mixer(EntryDraft entry) =>
        entry.Fields.OfType<NumberField>().First(field => field.Key == "videoMixer");

    private static MapField Map(EntryDraft entry) => entry.Fields.OfType<MapField>().First();
}
