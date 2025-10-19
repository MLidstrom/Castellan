# Notification Services

Castellan provides multiple channels for real-time security alert delivery:

## Teams & Slack Integration

### Microsoft Teams
- **Rich Adaptive Cards** with detailed security event information
- **User Mentions** for critical alerts
- **Action Buttons** for quick response workflows
- **Formatted Severity Levels** with color coding

### Slack
- **Block Kit Formatted Messages** with structured layouts
- **User Mentions** and **Channel Notifications**
- **Action Buttons** for immediate response
- **Rich Context** with event details and recommendations

### Webhook Management
- **Full CRUD Interface** for webhook configuration in the admin dashboard
- **Multiple Webhooks** - Configure separate channels for different alert types
- **URL Validation** - Only official Teams/Slack webhook domains accepted
- **Connection Testing** - Built-in connectivity testing from admin interface

### Rate Limiting & Throttling
Configurable throttling per severity level to prevent notification spam:

- **Critical Alerts**: Instant delivery with user mentions
- **High Alerts**: 5-minute throttling window
- **Medium Alerts**: 15-minute throttling window  
- **Low Alerts**: 60-minute throttling window

## Desktop & Web Notifications

### Desktop Alerts
- **Real-time System Tray Notifications** for critical security events
- **Windows Native Integration** with system notification center
- **Severity-based Icons** and sound alerts
- **Click-through Actions** to web dashboard

### Web Dashboard
- **React-based Admin Interface** with live event monitoring
- **Real-time Updates** via SignalR WebSocket connections
- **Interactive Event Timeline** with filtering and search
- **Live System Health** monitoring and status indicators

### Email Integration
- **SMTP Support** for email alerts (configurable)
- **HTML Email Templates** with rich formatting
- **Attachment Support** for detailed reports
- **Distribution Lists** and role-based routing

## Configuration

### Access Admin Interface
Navigate to `http://localhost:3000` for comprehensive notification settings management.

### Notification Settings Configuration

1. **Add Webhook URLs**:
   - Navigate to "Configuration" → "Notifications" tab in the admin interface
   - Add your Teams or Slack webhook URLs
   - Configure notification types and rate limits for each service

2. **Rate Limiting Configuration**:
   - Set throttling windows per severity level
   - Configure burst limits for high-volume scenarios
   - Enable/disable notifications per channel

3. **Test Connections**:
   - Use built-in connectivity testing
   - Verify webhook endpoints are reachable
   - Test message formatting and delivery

4. **Channel Routing**:
   - Route different event types to specific channels
   - Configure user mentions for critical alerts
   - Set up escalation policies

### Getting Webhook URLs

#### Microsoft Teams
1. Navigate to the Teams channel where you want alerts
2. Click the **"..."** menu → **Connectors**
3. Search for **"Incoming Webhook"** → **Configure**
4. Provide a name (e.g., "Castellan Security Alerts")
5. Upload an icon (optional)
6. Click **Create** → **Copy** the webhook URL

#### Slack
1. Navigate to your Slack workspace
2. Go to **Apps** → Search for **"Incoming Webhooks"**
3. Click **Add to Slack** → **Add Incoming WebHooks Integration**
4. Choose the channel for alerts
5. **Copy** the webhook URL provided

### Example Configuration

```powershell
# Configure via web interface or environment variables
$env:NOTIFICATIONS__TEAMS__WEBHOOKURL = "https://your-tenant.webhook.office.com/..."
$env:NOTIFICATIONS__SLACK__WEBHOOKURL = "https://hooks.slack.com/services/..."
$env:NOTIFICATIONS__RATELIMITING__CRITICAL__INTERVALMINUTES = "0"   # Instant
$env:NOTIFICATIONS__RATELIMITING__HIGH__INTERVALMINUTES = "5"       # 5 minutes
$env:NOTIFICATIONS__RATELIMITING__MEDIUM__INTERVALMINUTES = "15"    # 15 minutes
$env:NOTIFICATIONS__RATELIMITING__LOW__INTERVALMINUTES = "60"       # 1 hour
```

## Alert Types & Formats

### Critical Security Events
- **Immediate Delivery** with user mentions
- **Rich Context** including event details, affected systems, and recommended actions
- **Action Buttons** for immediate response (acknowledge, investigate, escalate)

### Threat Intelligence Alerts
- **Malware Detection** notifications with VirusTotal analysis results
- **IP Reputation** alerts with geolocation and threat scoring
- **IOC Matches** with context from MalwareBazaar and AlienVault OTX

### System Health Notifications
- **Service Status Changes** (Qdrant, AI providers, threat intelligence)
- **Performance Threshold Breaches** with real-time metrics
- **Configuration Changes** and validation results

### Sample Alert Formats

#### Teams Adaptive Card
```json
{
  "type": "AdaptiveCard",
  "body": [
    {
      "type": "TextBlock",
      "text": "🚨 Critical Security Event Detected",
      "weight": "Bolder",
      "size": "Large",
      "color": "Attention"
    },
    {
      "type": "FactSet",
      "facts": [
        {"title": "Event ID:", "value": "4625"},
        {"title": "Source:", "value": "Windows Security Log"},
        {"title": "Severity:", "value": "Critical"},
        {"title": "MITRE Technique:", "value": "T1110 - Brute Force"}
      ]
    }
  ],
  "actions": [
    {
      "type": "Action.OpenUrl",
      "title": "Investigate",
      "url": "http://localhost:3000/events/12345"
    }
  ]
}
```

#### Slack Block Kit
```json
{
  "blocks": [
    {
      "type": "header",
      "text": {
        "type": "plain_text",
        "text": "🚨 Critical Security Alert"
      }
    },
    {
      "type": "section",
      "fields": [
        {"type": "mrkdwn", "text": "*Event:*\nFailed Login Attempt"},
        {"type": "mrkdwn", "text": "*Severity:*\nCritical"},
        {"type": "mrkdwn", "text": "*System:*\nDC01.domain.com"},
        {"type": "mrkdwn", "text": "*Time:*\n2025-09-09 06:30:00 UTC"}
      ]
    },
    {
      "type": "actions",
      "elements": [
        {
          "type": "button",
          "text": {"type": "plain_text", "text": "Investigate"},
          "url": "http://localhost:3000/events/12345",
          "style": "primary"
        }
      ]
    }
  ]
}
```
