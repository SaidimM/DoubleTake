#!/usr/bin/env python3
"""
Generate Apple Live Photo (.JPG + .MOV) from any MP4/Video file.
Injects matching Apple Asset Identifiers into both the JPEG XMP metadata and QuickTime MOV metadata.
When saved or AirDropped to iOS Photos, iOS and Xiaohongshu (RedNote) recognize it as a native Live Photo (实况照片).
"""

import argparse
import os
import struct
import subprocess
import uuid


def make_live_photo(
    video_path: str,
    output_dir: str,
    base_name: str = "DoubleTake_LivePhoto",
    still_time_sec: float = 5.0,
    ffmpeg_path: str = r"C:\ffmpeg\ffmpeg-8.0.1-essentials_build\bin\ffmpeg.exe",
):
    os.makedirs(output_dir, exist_ok=True)
    asset_id = str(uuid.uuid4()).upper()
    print(f"[*] Creating Apple Live Photo pair...")
    print(f"    - Input Video: {video_path}")
    print(f"    - Asset UUID:  {asset_id}")
    print(f"    - Still Frame: t={still_time_sec}s")

    jpg_path = os.path.join(output_dir, f"{base_name}.JPG")
    mov_path = os.path.join(output_dir, f"{base_name}.MOV")

    # 1. Extract cover frame as high-quality JPEG
    cmd_extract = [
        ffmpeg_path,
        "-y",
        "-ss",
        str(still_time_sec),
        "-i",
        video_path,
        "-vframes",
        "1",
        "-q:v",
        "2",
        jpg_path,
    ]
    res_ext = subprocess.run(cmd_extract, capture_output=True, text=True)
    if res_ext.returncode != 0:
        raise RuntimeError(f"FFmpeg frame extraction failed: {res_ext.stderr}")

    # 2. Inject Apple ContentIdentifier XMP metadata into the JPEG
    with open(jpg_path, "rb") as f:
        jpg_data = f.read()

    xmp_packet = f"""<x:xmpmeta xmlns:x="adobe:ns:meta/">
 <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
  <rdf:Description rdf:about=""
    xmlns:apple="http://ns.apple.com/image/1.0/"
    xmlns:xmp="http://ns.adobe.com/xap/1.0/"
    apple:ContentIdentifier="{asset_id}"/>
 </rdf:RDF>
</x:xmpmeta>"""

    xmp_header = b"http://ns.adobe.com/xap/1.0/\x00"
    payload = xmp_header + xmp_packet.encode("utf-8")
    app1_marker = b"\xff\xe1" + struct.pack(">H", len(payload) + 2) + payload

    if jpg_data.startswith(b"\xff\xd8"):
        new_jpg_data = jpg_data[:2] + app1_marker + jpg_data[2:]
        with open(jpg_path, "wb") as f:
            f.write(new_jpg_data)
        print(f"[✓] Still Image ready: {jpg_path} ({len(new_jpg_data)/1024:.1f} KB)")
    else:
        raise ValueError("Invalid JPEG header")

    # 3. Create paired MOV container with matching QuickTime metadata
    still_time_ms = int(still_time_sec * 1000)
    cmd_mov = [
        ffmpeg_path,
        "-y",
        "-i",
        video_path,
        "-movflags",
        "+use_metadata_tags",
        "-metadata",
        f"com.apple.quicktime.content.identifier={asset_id}",
        "-metadata",
        f"com.apple.quicktime.still-image-time={still_time_ms}",
        "-c:v",
        "copy",
        "-c:a",
        "copy",
        mov_path,
    ]
    res_mov = subprocess.run(cmd_mov, capture_output=True, text=True)
    if res_mov.returncode != 0:
        raise RuntimeError(f"FFmpeg MOV generation failed: {res_mov.stderr}")

    print(f"[✓] Video Component ready: {mov_path} ({os.path.getsize(mov_path)/1024:.1f} KB)")
    print(f"\n[🎉] Live Photo generated successfully in: {output_dir}")
    print(f"     -> Transfer both `{base_name}.JPG` and `{base_name}.MOV` to your iPhone (AirDrop or iCloud)")
    print(f"     -> iOS Photos will automatically merge them into a single Live Photo (实况照片)!")


def main():
    parser = argparse.ArgumentParser(description="Convert MP4 Video to Apple Live Photo")
    parser.add_argument("--video", default=r"assets/demo_rednote_3x4.mp4", help="Input video file")
    parser.add_argument("--out_dir", default=r"assets/livephoto_3x4", help="Output directory")
    parser.add_argument("--name", default="DoubleTake_RedNote_LivePhoto", help="Base file name")
    parser.add_argument("--still_time", type=float, default=5.0, help="Cover frame time in seconds")
    args = parser.parse_args()

    make_live_photo(
        video_path=os.path.abspath(args.video),
        output_dir=os.path.abspath(args.out_dir),
        base_name=args.name,
        still_time_sec=args.still_time,
    )


if __name__ == "__main__":
    main()
