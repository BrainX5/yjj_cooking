# Cooking Audio Guide

This project now includes a procedural fallback audio system in `CookingAudioController.cs`.
If no real `AudioClip` is assigned, the game will generate simple placeholder sounds at runtime.

## Recommended Real Audio Replacements

Use short, clean, stylized one-shot sounds. Keep most clips under `0.5s`.

### 1. Mushroom Harvest
- `pickupClip`
- Suggested sound: soft leaf rustle + tiny magical pluck
- Use when: player picks a mushroom near the house
- Ideal length: `0.10s - 0.20s`

### 2. Mushroom Into Pot
- `mushroomDropClip`
- Suggested sound: light veggie/plop/drop into soup
- Use when: adding mushroom into the pot
- Ideal length: `0.12s - 0.22s`

### 3. Stirring
- `stirClip`
- Suggested sound: wooden stick stirring thick soup, soft liquid swirl
- Use when: left/right stir action completes
- Ideal length: `0.15s - 0.30s`

### 4. Fire Control
- `fireTapClip`
- Suggested sound: tiny fire crackle or ember puff
- Use when: pressing `Space` to boost fire
- Ideal length: `0.05s - 0.12s`

### 5. Fish Catch Press
- `fishPressClip`
- Suggested sound: quick water flick / tension tick
- Use when: player mashes `Space` while catching fish
- Ideal length: `0.04s - 0.10s`

### 6. Fish Escape
- `fishEscapeClip`
- Suggested sound: splash + disappointed down tone
- Use when: fish escapes
- Ideal length: `0.18s - 0.35s`

### 7. Fish Caught
- `fishCaughtClip`
- Suggested sound: happy splash + success sparkle
- Use when: fish is successfully caught
- Ideal length: `0.20s - 0.40s`

### 8. Flip Fish
- `fishFlipClip`
- Suggested sound: pan flip / soft fry slap
- Use when: left/right flip completes during fried fish stage
- Ideal length: `0.12s - 0.25s`

### 9. Seasoning
- `seasoningClip`
- Suggested sound: sprinkle / salt shake / herb dust
- Use when: up/down seasoning completes
- Ideal length: `0.08s - 0.18s`

### 10. Dish Complete
- `dishCompleteClip`
- Suggested sound: warm cooking success chime
- Use when: mushroom soup completed or fried fish completed
- Ideal length: `0.25s - 0.50s`

### 11. UI Close / Start Frying
- `uiCloseClip`
- Suggested sound: gentle paper close / confirm blip
- Use when: returning to the pot and entering the fried fish sequence
- Ideal length: `0.06s - 0.15s`

## Folder Recommendation

If you want a clean structure, place clips under:

```text
Assets/Audio/Cooking/
```

Suggested filenames:

```text
Assets/Audio/Cooking/pickup_mushroom.wav
Assets/Audio/Cooking/drop_mushroom.wav
Assets/Audio/Cooking/stir_soup.wav
Assets/Audio/Cooking/fire_tap.wav
Assets/Audio/Cooking/fish_press.wav
Assets/Audio/Cooking/fish_escape.wav
Assets/Audio/Cooking/fish_caught.wav
Assets/Audio/Cooking/fish_flip.wav
Assets/Audio/Cooking/seasoning.wav
Assets/Audio/Cooking/dish_complete.wav
Assets/Audio/Cooking/ui_close.wav
```

## How To Replace

1. Select the `CookingAudioController` object in Unity.
2. Drag real `AudioClip` assets into the matching fields.
3. Leave any field empty if you still want the procedural fallback for that sound.

## Style Notes

Best fit for this project:
- soft and playful
- forest / rustic / hand-crafted
- not realistic AAA cooking
- avoid harsh UI beeps
- avoid long reverb tails
