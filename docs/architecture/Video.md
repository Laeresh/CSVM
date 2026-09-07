# Video

The managed MPEG-1 decoder for the ten `.mpg` cinemas the retail install ships: the system-stream
demultiplexer and the video decoder under it, plus the pure pieces they are built from. No type
here touches the engine, which is what lets a plain unit test play a whole file.

One `## src/...` entry per module, body at most 8 lines.

Traps do not live here; the rule is in `docs/architecture.md`. The container and codec facts, the
per-file parameters and where the files are named from are in
[../formats/cinemas.md](../formats/cinemas.md).

## src/Video/MpegMovie.cs
The surface a caller holds: `FromFile` or `FromBytes` opens one cinema, `Width`, `Height`,
`FrameRate` and `PixelAspectRatio` report what that file's own headers declare, `NextFrame`
hands out the next picture in display order, and `Rewind` restarts a looping one. `AudioPackets`
is the demultiplexed audio elementary stream, timestamped and undecoded, which is where a layer
II decoder attaches. It owns an `MpegSystemStream` and an `MpegVideoDecoder` and adds nothing of
its own beyond joining them, so a caller that needs the packets or the picture stream separately
can take either directly. Read `MpegSystemStream.cs` next.

## src/Video/MpegSystemStream.cs
Walks a whole system stream in memory: packs, system headers, and the packets of each elementary
stream, with each packet's presentation timestamp off the container's 90 kHz clock. The video
packets are joined into one buffer because the video decoder reads across packet boundaries; the
audio packets stay separate because a decoder for them needs each one's timestamp. `MpegPacket`
is the packet as a window into the file's own bytes rather than a copy, so the file array stays
alive as long as the packets do. Read `MpegVideoDecoder.cs` next.

## src/Video/MpegVideoDecoder.cs
The decoder proper: sequence header, picture, slice, macroblock and block, over an
`MpegBitReader`. It keeps the three pictures the standard's prediction needs and rotates them, so
a reference picture is held back one call and comes out after the bidirectional pictures that
precede it in display order; the last one falls out at the end of the stream. Frame times are the
picture's ordinal over the sequence's own rate, offset by the container's start time. Every
parameter comes from the stream's own headers, because two of the ten files differ from the
other eight. Read `DctBlock.cs` and `MotionCompensation.cs` next.

## src/Video/MpegBitReader.cs
The bit-level reader every symbol is read through: fixed-width fields, byte alignment, stuffing
runs, the `00 00 01` start-code scan, the 23-bit lookahead that ends a slice, and the
variable-length code walk over a `VideoVlcTables` table. Reading past the end yields zero and
does not move, so a truncated file ends the decode rather than throwing. Read
`VideoVlcTables.cs` next.

## src/Video/VideoVlcTables.cs
The variable-length code tables of ISO 11172-2 as data: address increment, the three macroblock
type tables, coded block pattern, motion code, the two DC size tables and the run/level table.
Each is a binary tree flattened into (next, value) pairs that `MpegBitReader.ReadVlc` walks one
bit at a time. `CSVM.Tests/VideoVlcTableTests.cs` checks individual codes against the standard's
own tables and checks each tree for reachability, which is the only defence against a table that
is wrong by one and decodes to plausible rubbish.

## src/Video/DctBlock.cs
The 8x8 block: the zig-zag scan order, the two default quantiser matrices, the per-coefficient
scale factors the transform folds in, `Dequantize` and `InverseTransform`. The transform is the
integer one decoders use in place of the standard's real-valued definition, and its scale factors
are held to eight bits, so its output is within the mismatch the standard permits rather than
exact. Read `MpegVideoDecoder.cs` for the caller.

## src/Video/MotionCompensation.cs
`Predict` fetches one macroblock of one plane from a reference picture and writes or averages it
into the current one. Vectors are in half-pel units, so an odd component averages the samples it
falls between; at a plane edge the missing neighbour repeats. A vector whose block falls outside
the plane predicts nothing, which is how a corrupt stream degrades instead of throwing.

## src/Video/VideoFrame.cs
One decoded picture: the three 4:2:0 planes padded out to whole macroblocks, the picture size
inside them, and the presentation time. `WriteRgba` and `ToRgba` are the BT.601 conversion a
caller that wants pixels uses. The decoder hands out the same three frames over and over, so a
frame is valid only until the next one is asked for.
