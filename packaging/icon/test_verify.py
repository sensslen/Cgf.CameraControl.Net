import struct
import unittest
from unittest.mock import patch
import zlib

from verify import ROOT, png_size, unpack_packbits, verify_assets


def chunk(kind, payload):
    return (struct.pack(">I", len(payload)) + kind + payload
            + struct.pack(">I", zlib.crc32(kind + payload)))


def png(payloads, size=2):
    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header)
            + b"".join(chunk(b"IDAT", payload) for payload in payloads) + chunk(b"IEND", b""))


class PngImageData(unittest.TestCase):
    def test_split_image_data_decodes(self):
        compressed = zlib.compress(bytes(2 * (1 + 2 * 4)))
        self.assertEqual(png_size(png([compressed[:5], compressed[5:]])), (2, 2))

    def test_bad_image_data_with_valid_checksums_is_rejected(self):
        compressed = zlib.compress(bytes(2 * (1 + 2 * 4)))
        for payload in [b"not-zlib", compressed[:-1], compressed + b"extra",
                        zlib.compress(b"\x00"), zlib.compress(b"\x05" + bytes(17))]:
            with self.subTest(payload=payload), self.assertRaises((ValueError, zlib.error)):
                png_size(png([payload]))


class ClassicIcnsImageData(unittest.TestCase):
    def test_asset_verification_rejects_malformed_classic_streams(self):
        path = ROOT / "packaging" / "icon" / "AppIcon.icns"
        data = path.read_bytes()
        chunks = bytearray()
        offset = 8
        while offset < len(data):
            kind, length = struct.unpack_from(">4sI", data, offset)
            payload = b"\x80" if kind == b"is32" else data[offset + 8:offset + length]
            chunks.extend(kind + struct.pack(">I", len(payload) + 8) + payload)
            offset += length
        corrupt = b"icns" + struct.pack(">I", len(chunks) + 8) + chunks
        original = type(path).read_bytes

        def read_bytes(candidate):
            return corrupt if candidate == path else original(candidate)

        with patch.object(type(path), "read_bytes", read_bytes):
            with self.assertRaisesRegex(ValueError, "Truncated ICNS repeat"):
                verify_assets()

    def test_literals_and_repeats_decode(self):
        self.assertEqual(unpack_packbits(b"\x00\x10\x80\x20", 4), b"\x10\x20\x20\x20")
        self.assertEqual(unpack_packbits(b"\xff\x20", 130), b"\x20" * 130)

    def test_truncated_and_wrong_length_streams_are_rejected(self):
        for data, expected in [(b"\x02\x10\x20", 3), (b"\x80", 3),
                               (b"\x00\x10", 2), (b"\xff\x10", 1)]:
            with self.subTest(data=data), self.assertRaises(ValueError):
                unpack_packbits(data, expected)


class CaptionArtwork(unittest.TestCase):
    def test_blank_caption_with_correct_dimensions_is_rejected(self):
        caption_path = ROOT / "src" / "Cgf.CameraControl.App" / "Assets" / "appicon.png"
        blank = png([zlib.compress(bytes(64 * (1 + 64 * 4)))], size=64)
        self.assertEqual(png_size(blank), (64, 64))
        original = type(caption_path).read_bytes

        def read_bytes(path):
            return blank if path == caption_path else original(path)

        with patch.object(type(caption_path), "read_bytes", read_bytes):
            with self.assertRaisesRegex(ValueError, "Caption artwork differs"):
                verify_assets()


if __name__ == "__main__":
    unittest.main()
