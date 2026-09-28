# NG PVP PC Check — Render test deployment

This package keeps the working NO-PIN architecture and adds Render/Docker support.

## What this deployment does
- Public HTTPS server URL supplied by Render.
- Player Agent registers automatically; no PIN.
- Staff can see `PLAYER-XXXXXX` and start a check.
- Player Agent scans locally and uploads results.

## Important
The current server stores sessions/results in memory. A Render restart or free-tier sleep clears them.
Render free web services also spin down after 15 minutes without inbound traffic and take about a minute to wake up.
This package is for testing, not production.

## Deploy
1. Put this folder in a GitHub repository.
2. In Render: New -> Web Service -> connect the repository.
3. Select Docker runtime and Free plan.
4. Deploy.
5. Render gives a public `https://....onrender.com` URL.
6. Test the URL in a browser; it should show `NG PVP PC Check Server OK`.

## Next step after the Render URL exists
The Agent and Staff apps still default to localhost in the NO-PIN FIX6 build. After the final Render URL is known, rebuild them so the player EXE and Staff app point to that URL automatically.
