# Liquid brand

- `liquid-mark.svg` / `liquid-mark.png` – the swirl mark in the logo's own colours (four bands: violet, magenta to pink, yellow to orange, orange through magenta to violet). The same shapes as `Icons.Logo`, `favicon.svg`, `favicon.ico` and `apple-touch-icon.png`.
- `liquid-logo.svg` – the mark with the word "liquid" in white, for dark backgrounds. Everything is outlines (no font needed), traced from the logo artwork.

In the app the mark is inline SVG (`Icons.Logo`) coloured by each theme's `--logo-1` … `--logo-4`, and the word is `server/www/wordmark.svg` used as a CSS mask in the theme's colour. Regenerate these files from `Icons.Logo` if the mark changes.
