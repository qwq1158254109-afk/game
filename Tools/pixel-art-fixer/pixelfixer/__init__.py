"""pixelfixer detector - final consolidated grid detector.

detect(rgba) -> dict(step_x, step_y, cols, rows, phase_x, phase_y, consensus)

Architecture (see KNOWLEDGE.md for the full campaign that produced it):
  1. three cheap phase-free detectors run first:
       autocorr    - banded-ACF comb-anticomb + peak-train LS refinement
       runlengths  - multi-lag boundary-distance combs
       selfsim     - shift-dissimilarity ensemble with tile voting
     if they agree on the output size, done (fast path, ~1-2s).
  2. otherwise the evidence stack arbitrates per axis:
       fusion      - sum-fused channel curves (ray/tile/spectral)
       varcontrast - within-cell variance "square packer"
       reconsearch - distillability score (octave/harmonic arbiter)
     with agreement bonuses, smallest-qualified-peak, prefer-fine on wild
     axis disagreement, square snapping, and drift-aware counting.
"""

from .core import detect

__all__ = ["detect"]
