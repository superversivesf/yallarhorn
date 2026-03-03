# Setting Up YouTube Cookies for yt-dlp

## Why Do I Need This?

YouTube sometimes requires you to sign in to verify you're not a bot. When this happens, yt-dlp will fail with errors like:

```
ERROR: [youtube] SIGN_IN_TO_CONFIRM: Sign in to confirm you're not a bot
```

The solution is to provide yt-dlp with cookies from a browser where you're already logged into YouTube.

## Quick Setup

### Step 1: Install a Browser Extension

You need to export cookies from your browser. Install one of these extensions:

**Chrome / Edge / Brave:**
- [Get cookies.txt LOCALLY](https://chromewebstore.google.com/detail/get-cookiestxt-locally/cclelndahbckbenkjhflpdbgdldlbecc)

**Firefox:**
- [cookies.txt](https://addons.mozilla.org/en-US/firefox/addon/cookies-txt/)

### Step 2: Log Into YouTube

1. Open YouTube in your browser
2. Log in with your Google account
3. Verify you can watch videos normally

### Step 3: Export Cookies

1. Click the extension icon in your browser toolbar
2. Click "Export" or "Download cookies.txt"
3. Save the file as `cookies.txt`

### Step 4: Configure Yallarhorn

**Option A: Environment Variable**

```bash
export YALLARHORN_YTDLP_COOKIES_PATH=/path/to/cookies.txt
```

**Option B: appsettings.json**

```json
{
  "Ytdlp": {
    "CookiesPath": "/path/to/cookies.txt"
  }
}
```

**Option C: Docker**

1. Copy `cookies.txt` to your config directory:
   ```bash
   cp cookies.txt ./config/cookies.txt
   ```

2. Mount the config directory in `docker-compose.yml`:
   ```yaml
   volumes:
     - ./config:/app/config:ro
   ```

3. Set the environment variable in `.env`:
   ```
   YALLARHORN_YTDLP_COOKIES_PATH=/app/config/cookies.txt
   ```

### Step 5: Restart Yallarhorn

Restart the service to pick up the new configuration. You should see in the logs:

```
yt-dlp configured with cookies from /path/to/cookies.txt
```

## Important Notes

### Security

- **Keep cookies.txt private** - It contains your YouTube authentication
- **Don't commit it to git** - Add it to your `.gitignore`
- **Regenerate periodically** - YouTube cookies expire eventually

### Cookie Expiration

YouTube cookies typically expire after some time (days to months depending on your account settings). If you start getting authentication errors again:

1. Log back into YouTube in your browser
2. Re-export the cookies file
3. Restart Yallarhorn

### Docker Permissions

If running in Docker, ensure the container can read the cookies file:

```bash
# Make cookies readable
chmod 644 cookies.txt

# Or set ownership to match container user
chown 1000:1000 cookies.txt
```

## Testing

To verify cookies are working, check the logs when Yallarhorn starts:

- ✅ Good: `yt-dlp configured with cookies from /path/to/cookies.txt`
- ❌ Bad: `Cookies file not found at /path/to/cookies.txt`

## Troubleshooting

**"Cookies file not found"**
- Check the path is correct
- Check file permissions
- If using Docker, verify the volume mount

**Still getting sign-in errors**
- Ensure you're logged into YouTube in the browser you exported from
- Try logging out and back into YouTube
- Re-export the cookies file

**"Permission denied"**
- Check file permissions with `ls -la cookies.txt`
- Docker containers typically run as UID 1000