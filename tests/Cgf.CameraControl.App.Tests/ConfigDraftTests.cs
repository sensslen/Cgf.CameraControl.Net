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

    public class WhatStopsASave : ConfigDraftTests
    {
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
