#ifndef CSKY_TIME_INC
#define CSKY_TIME_INC

// THE clock every animated shader reads: seconds, written once per rendered frame from the
// session's simulation clock (CSVM.Utils.ShaderTime over GameClock). Nothing in this project
// uses Godot's TIME built-in, because TIME runs on wall time and would keep the water scrolling
// and the rain falling through a halted sim — and would make a fixed-step run's pixels depend on
// how fast the machine rendered it instead of on the frame count.
//
// ⚠ It wraps at the same 3600 s period as TIME (rendering/limits/time/time_rollover_secs). Every
// UV scroll rate in this install (0.07 / 0.4 / 0.5 / 0.7 / 1.0 units/s) times 3600 is a whole
// number of texture repeats, so the wrap lands on an identical frame. A different period puts a
// visible jump on every scrolling surface — see ShaderTime.RolloverSecs.

// Declared here ONLY — a global uniform's type must agree across every shader that declares
// it, so a second declaration elsewhere risks a mismatch the compiler will not catch for you.
global uniform float csky_time;

#endif
