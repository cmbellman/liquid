# Liquid brand

- `liquid-mark.svg` – the swirl mark in the logo's own colours (violet, magenta, orange, yellow), the same shape as `Icons.Logo` and `server/www/favicon.ico`.
- `liquid-logo.svg` – the mark with the word "liquid" in Exo 2 ExtraBold Italic, white, for dark backgrounds. The word is text, so it needs the font (loaded from Google Fonts when the SVG is opened in a browser); convert it to outlines in an editor before sending it to print.

In the app the mark is inline SVG (`Icons.Logo`) coloured by each theme's `--logo-1` … `--logo-4`. Regenerate these files from `Icons.Logo` if the mark changes.
