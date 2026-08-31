Junhan Audio Integration Pack v1
================================
Target branch: Junhan2
Target Unity path: Assets/Junhan/Audio/

Included implementation audio:
- BGM: 1
- SFX: 33
- Total: 34

Important:
- All 34 implementation files from the supplied confirmed list are included.
- PREVIEWS/00_preview_*.wav files are intentionally NOT imported into Assets because they are audition/reference composites, not runtime assets.
- Original file names are preserved.
- Existing project audio such as lobby/ingame/mini-stage/boss BGM and previously integrated SFX should remain in their current locations to avoid breaking Unity references.

Suggested import settings:
- SFX: Decompress On Load, 2D, Play On Awake off on runtime AudioSources.
- augment_selection_bgm.wav: Streaming + Vorbis, Stereo, loop controlled by GameAudioManager.

Next code integration:
1. Keep current GameAudioManager.
2. Add the new SFX IDs/AudioClip slots.
3. Add a modal/selection BGM layer for augment_selection_bgm.
4. Wire events to actual successful actions, not input attempts.
