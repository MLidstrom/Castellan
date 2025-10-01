import React, { useState, useEffect, useCallback } from 'react';
import {
  Snackbar,
  Alert,
  Badge,
  IconButton,
  Popover,
  List,
  ListItem,
  ListItemAvatar,
  ListItemText,
  Avatar,
  Typography,
  Box,
  Button,
  Chip,
  Divider,
  Card,
  CardContent,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
  Switch,
  FormControlLabel
} from '@mui/material';
import {
  Notifications as NotificationsIcon,
  Security as SecurityIcon,
  Warning as WarningIcon,
  Error as ErrorIcon,
  Info as InfoIcon,
  CheckCircle as SuccessIcon,
  Close as CloseIcon,
  Settings as SettingsIcon,
  MarkEmailRead as MarkReadIcon,
  Delete as DeleteIcon,
  Circle as CircleIcon
} from '@mui/icons-material';
import { useSignalRContext } from '../contexts/SignalRContext';

interface SystemNotification {
  id: string;
  type: 'security' | 'system' | 'performance';
  severity: 'info' | 'warning' | 'error' | 'success';
  title: string;
  message: string;
  timestamp: string;
  read: boolean;
  dismissible: boolean;
  actions?: NotificationAction[];
  metadata?: {
    resource?: string;
    recordId?: string | number;
    riskLevel?: string;
    [key: string]: any;
  };
}

interface NotificationAction {
  label: string;
  action: 'navigate' | 'dismiss' | 'custom';
  data?: any;
}

interface NotificationSettings {
  enabled: boolean;
  soundEnabled: boolean;
  desktopEnabled: boolean;
  severityFilter: SystemNotification['severity'][];
  typeFilter: SystemNotification['type'][];
  autoMarkReadDelay: number; // seconds
}

interface NotificationSystemProps {
  maxNotifications?: number;
  enableRealtime?: boolean;
  position?: 'top-right' | 'top-left' | 'bottom-right' | 'bottom-left';
}

const defaultSettings: NotificationSettings = {
  enabled: true,
  soundEnabled: true,
  desktopEnabled: true,
  severityFilter: ['info', 'warning', 'error', 'success'],
  typeFilter: ['security', 'system', 'performance'],
  autoMarkReadDelay: 5
};

export const NotificationSystem: React.FC<NotificationSystemProps> = ({
  maxNotifications = 100,
  enableRealtime = true,
  position = 'top-right'
}) => {
  const [notifications, setNotifications] = useState<SystemNotification[]>([]);
  const [anchorEl, setAnchorEl] = useState<HTMLButtonElement | null>(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [settings, setSettings] = useState<NotificationSettings>(defaultSettings);
  const [snackbarOpen, setSnackbarOpen] = useState(false);
  const [currentSnackbar, setCurrentSnackbar] = useState<SystemNotification | null>(null);

  // Load settings from localStorage
  useEffect(() => {
    const savedSettings = localStorage.getItem('notificationSettings');
    if (savedSettings) {
      try {
        setSettings({ ...defaultSettings, ...JSON.parse(savedSettings) });
      } catch (error) {
        console.error('Failed to load notification settings:', error);
      }
    }
  }, []);

  // Save settings to localStorage
  const saveSettings = useCallback((newSettings: NotificationSettings) => {
    localStorage.setItem('notificationSettings', JSON.stringify(newSettings));
    setSettings(newSettings);
  }, []);

  // Request desktop notification permission
  useEffect(() => {
    if (settings.desktopEnabled && 'Notification' in window && Notification.permission === 'default') {
      Notification.requestPermission();
    }
  }, [settings.desktopEnabled]);

  // SignalR integration for real-time notifications
  const { isConnected } = useSignalRContext();

  const addNotification = useCallback((notification: SystemNotification) => {
    // Check if notification should be shown based on settings
    if (!settings.enabled || 
        !settings.severityFilter.includes(notification.severity) ||
        !settings.typeFilter.includes(notification.type)) {
      return;
    }

    setNotifications(prev => {
      const updated = [notification, ...prev].slice(0, maxNotifications);
      return updated;
    });

    // Show snackbar notification
    setCurrentSnackbar(notification);
    setSnackbarOpen(true);

    // Play sound
    if (settings.soundEnabled) {
      playNotificationSound(notification.severity);
    }

    // Show desktop notification
    if (settings.desktopEnabled && 'Notification' in window && Notification.permission === 'granted') {
      new Notification(notification.title, {
        body: notification.message,
        icon: getNotificationIcon(notification.type),
        tag: notification.id
      });
    }

    // Auto-mark as read after delay
    if (settings.autoMarkReadDelay > 0) {
      setTimeout(() => {
        markAsRead(notification.id);
      }, settings.autoMarkReadDelay * 1000);
    }
  }, [settings, maxNotifications]);

  const playNotificationSound = (severity: SystemNotification['severity']) => {
    try {
      const audio = new Audio();
      switch (severity) {
        case 'error':
          audio.src = '/sounds/error.mp3';
          break;
        case 'warning':
          audio.src = '/sounds/warning.mp3';
          break;
        case 'success':
          audio.src = '/sounds/success.mp3';
          break;
        default:
          audio.src = '/sounds/notification.mp3';
      }
      audio.play().catch(() => {
        // Ignore audio play errors (browser restrictions)
      });
    } catch (error) {
      // Ignore audio errors
    }
  };

  const getNotificationIcon = (type: SystemNotification['type']) => {
    switch (type) {
      case 'security':
        return '/icons/security-notification.png';
      case 'performance':
        return '/icons/performance-notification.png';
      default:
        return '/icons/system-notification.png';
    }
  };

  const getSeverityIcon = (severity: SystemNotification['severity']) => {
    switch (severity) {
      case 'error':
        return <ErrorIcon color="error" />;
      case 'warning':
        return <WarningIcon color="warning" />;
      case 'success':
        return <SuccessIcon color="success" />;
      default:
        return <InfoIcon color="info" />;
    }
  };

  const getSeverityColor = (severity: SystemNotification['severity']) => {
    switch (severity) {
      case 'error':
        return '#f44336';
      case 'warning':
        return '#ff9800';
      case 'success':
        return '#4caf50';
      default:
        return '#2196f3';
    }
  };

  const markAsRead = useCallback((notificationId: string) => {
    setNotifications(prev => 
      prev.map(n => n.id === notificationId ? { ...n, read: true } : n)
    );
  }, []);

  const markAllAsRead = useCallback(() => {
    setNotifications(prev => prev.map(n => ({ ...n, read: true })));
  }, []);

  const dismissNotification = useCallback((notificationId: string) => {
    setNotifications(prev => prev.filter(n => n.id !== notificationId));
  }, []);

  const clearAllNotifications = useCallback(() => {
    setNotifications([]);
  }, []);

  const handleNotificationClick = (notification: SystemNotification) => {
    if (!notification.read) {
      markAsRead(notification.id);
    }

    // Handle actions
    if (notification.actions && notification.actions.length > 0) {
      const primaryAction = notification.actions[0];
      if (primaryAction.action === 'navigate' && primaryAction.data?.path) {
        window.location.href = primaryAction.data.path;
      }
    }
  };

  const handlePopoverOpen = (event: React.MouseEvent<HTMLButtonElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handlePopoverClose = () => {
    setAnchorEl(null);
  };

  const unreadCount = notifications.filter(n => !n.read).length;
  const open = Boolean(anchorEl);

  // Demo function to test notifications
  const addTestNotification = (type: SystemNotification['type'], severity: SystemNotification['severity']) => {
    const testNotification: SystemNotification = {
      id: Date.now().toString(),
      type,
      severity,
      title: `Test ${type} ${severity}`,
      message: `This is a test ${severity} notification for ${type} events.`,
      timestamp: new Date().toISOString(),
      read: false,
      dismissible: true,
      actions: [
        {
          label: 'View Details',
          action: 'navigate',
          data: { path: `/${type}` }
        }
      ],
      metadata: {
        resource: type,
        riskLevel: severity === 'error' ? 'high' : 'medium'
      }
    };

    addNotification(testNotification);
  };

  return (
    <>
      {/* Notification Bell Icon */}
      <IconButton
        color="inherit"
        onClick={handlePopoverOpen}
        sx={{ 
          color: unreadCount > 0 ? 'warning.main' : 'inherit'
        }}
      >
        <Badge badgeContent={unreadCount} color="error" max={99}>
          <NotificationsIcon />
        </Badge>
      </IconButton>

      {/* Notifications Popover */}
      <Popover
        open={open}
        anchorEl={anchorEl}
        onClose={handlePopoverClose}
        anchorOrigin={{
          vertical: 'bottom',
          horizontal: 'right',
        }}
        transformOrigin={{
          vertical: 'top',
          horizontal: 'right',
        }}
        PaperProps={{
          sx: { width: 400, maxHeight: 600 }
        }}
      >
        <Box sx={{ p: 2, borderBottom: 1, borderColor: 'divider' }}>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <Typography variant="h6">
              Notifications
              {isConnected && (
                <Chip
                  size="small"
                  label="Live"
                  color="success"
                  sx={{ ml: 1 }}
                />
              )}
            </Typography>
            <Box>
              <IconButton size="small" onClick={() => setSettingsOpen(true)}>
                <SettingsIcon fontSize="small" />
              </IconButton>
              {unreadCount > 0 && (
                <IconButton size="small" onClick={markAllAsRead}>
                  <MarkReadIcon fontSize="small" />
                </IconButton>
              )}
            </Box>
          </Box>
          
          {notifications.length > 0 && (
            <Box sx={{ mt: 1, display: 'flex', gap: 1 }}>
              <Button size="small" onClick={markAllAsRead}>
                Mark All Read
              </Button>
              <Button size="small" onClick={clearAllNotifications} color="error">
                Clear All
              </Button>
            </Box>
          )}
        </Box>

        <List sx={{ maxHeight: 400, overflow: 'auto', p: 0 }}>
          {notifications.length === 0 ? (
            <ListItem>
              <ListItemText 
                primary="No notifications"
                secondary="You're all caught up!"
              />
            </ListItem>
          ) : (
            notifications.map((notification, index) => (
              <React.Fragment key={notification.id}>
                <ListItem
                  sx={{
                    cursor: 'pointer',
                    backgroundColor: notification.read ? 'transparent' : 'action.hover',
                    '&:hover': { backgroundColor: 'action.selected' }
                  }}
                  onClick={() => handleNotificationClick(notification)}
                >
                  <ListItemAvatar>
                    <Avatar sx={{ bgcolor: getSeverityColor(notification.severity) }}>
                      {getSeverityIcon(notification.severity)}
                    </Avatar>
                  </ListItemAvatar>
                  <ListItemText
                    primary={
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
                        <Typography variant="subtitle2">
                          {notification.title}
                        </Typography>
                        {!notification.read && (
                          <CircleIcon sx={{ fontSize: 8, color: 'primary.main' }} />
                        )}
                        <Chip
                          size="small"
                          label={notification.type}
                          variant="outlined"
                          sx={{ ml: 'auto' }}
                        />
                      </Box>
                    }
                    secondary={
                      <Box>
                        <Typography variant="body2" color="textSecondary">
                          {notification.message}
                        </Typography>
                        <Typography variant="caption" color="textSecondary">
                          {new Date(notification.timestamp).toLocaleString()}
                        </Typography>
                      </Box>
                    }
                  />
                  {notification.dismissible && (
                    <IconButton
                      size="small"
                      onClick={(e) => {
                        e.stopPropagation();
                        dismissNotification(notification.id);
                      }}
                    >
                      <CloseIcon fontSize="small" />
                    </IconButton>
                  )}
                </ListItem>
                {index < notifications.length - 1 && <Divider />}
              </React.Fragment>
            ))
          )}
        </List>

        {/* Test Notifications (Development Only) */}
        {process.env.NODE_ENV === 'development' && (
          <Box sx={{ p: 2, borderTop: 1, borderColor: 'divider' }}>
            <Typography variant="caption" color="textSecondary" gutterBottom>
              Test Notifications (Dev Only)
            </Typography>
            <Box sx={{ display: 'flex', gap: 1, flexWrap: 'wrap' }}>
              <Button
                size="small"
                onClick={() => addTestNotification('security', 'error')}
                color="error"
              >
                Security Alert
              </Button>
              <Button
                size="small"
                onClick={() => addTestNotification('system', 'warning')}
                color="warning"
              >
                System Warning
              </Button>
              <Button
                size="small"
                onClick={() => addTestNotification('performance', 'info')}
                color="info"
              >
                Performance Info
              </Button>
            </Box>
          </Box>
        )}
      </Popover>

      {/* Settings Dialog */}
      <Popover
        open={settingsOpen}
        anchorEl={anchorEl}
        onClose={() => setSettingsOpen(false)}
        anchorOrigin={{
          vertical: 'bottom',
          horizontal: 'right',
        }}
        transformOrigin={{
          vertical: 'top',
          horizontal: 'right',
        }}
        PaperProps={{
          sx: { width: 350 }
        }}
      >
        <Card>
          <CardContent>
            <Typography variant="h6" gutterBottom>
              Notification Settings
            </Typography>
            
            <FormControlLabel
              control={
                <Switch
                  checked={settings.enabled}
                  onChange={(e) => saveSettings({ ...settings, enabled: e.target.checked })}
                />
              }
              label="Enable Notifications"
            />
            
            <FormControlLabel
              control={
                <Switch
                  checked={settings.soundEnabled}
                  onChange={(e) => saveSettings({ ...settings, soundEnabled: e.target.checked })}
                />
              }
              label="Sound Alerts"
            />
            
            <FormControlLabel
              control={
                <Switch
                  checked={settings.desktopEnabled}
                  onChange={(e) => saveSettings({ ...settings, desktopEnabled: e.target.checked })}
                />
              }
              label="Desktop Notifications"
            />
            
            <FormControl fullWidth margin="normal">
              <InputLabel>Auto Mark Read (seconds)</InputLabel>
              <Select
                value={settings.autoMarkReadDelay}
                onChange={(e) => saveSettings({ ...settings, autoMarkReadDelay: Number(e.target.value) })}
                label="Auto Mark Read (seconds)"
              >
                <MenuItem value={0}>Never</MenuItem>
                <MenuItem value={5}>5 seconds</MenuItem>
                <MenuItem value={10}>10 seconds</MenuItem>
                <MenuItem value={30}>30 seconds</MenuItem>
                <MenuItem value={60}>1 minute</MenuItem>
              </Select>
            </FormControl>
          </CardContent>
        </Card>
      </Popover>

      {/* Snackbar for New Notifications */}
      <Snackbar
        open={snackbarOpen}
        autoHideDuration={6000}
        onClose={() => setSnackbarOpen(false)}
        anchorOrigin={{
          vertical: position.includes('top') ? 'top' : 'bottom',
          horizontal: position.includes('right') ? 'right' : 'left'
        }}
      >
        <Alert 
          onClose={() => setSnackbarOpen(false)} 
          severity={currentSnackbar?.severity || 'info'}
          variant="filled"
        >
          <Typography variant="subtitle2">
            {currentSnackbar?.title}
          </Typography>
          {currentSnackbar?.message}
        </Alert>
      </Snackbar>
    </>
  );
};