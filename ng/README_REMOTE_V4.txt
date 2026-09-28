NG PVP REMOTE PC CHECK - PIN ONLY FLOW

PLAYER:
1) Player runs the Agent.
2) Agent connects to the NG PVP server and displays a 6-digit PIN.
3) Player sends ONLY that PIN to staff.

STAFF:
1) Staff opens the dashboard.
2) Staff enters ONLY the player PIN.
3) CONNECT, then START PC CHECK.
4) Results appear automatically when the Agent finishes.

CURRENT TEST SERVER:
http://localhost:5090

PRODUCTION:
Use a public HTTPS server URL. The Staff dashboard has the server URL compiled into the app; the Agent can receive the public server URL as its first argument. Do not expose the local-test localhost URL to remote players.

IMPORTANT:
This tool is for transparent anti-cheat inspection. It does not collect passwords, cookies, tokens, or credentials.
