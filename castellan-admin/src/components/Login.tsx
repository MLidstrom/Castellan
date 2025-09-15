import React, { useState } from 'react';
import {
  useLogin,
  useNotify,
  useSafeSetState,
  Form,
  required,
  TextInput,
  PasswordInput,
  Button as AdminButton
} from 'react-admin';
import {
  Box,
  Card,
  CardContent,
  Typography,
  Container,
  Avatar,
  CircularProgress
} from '@mui/material';
import {
  Security as SecurityIcon
} from '@mui/icons-material';

export const Login: React.FC = () => {
  const [loading, setLoading] = useSafeSetState(false);
  const login = useLogin();
  const notify = useNotify();

  const handleSubmit = async (data: any) => {
    setLoading(true);
    try {
      await login({ username: data.username, password: data.password });
    } catch (error) {
      // Suppress error notifications - login page should be clean
      console.log('Login failed:', error);
    } finally {
      setLoading(false);
    }
  };

  return (
    <Container component="main" maxWidth="sm">
      <Box
        sx={{
          minHeight: '100vh',
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          py: 3,
        }}
      >
        <Card elevation={8} sx={{ width: '100%', maxWidth: 400 }}>
          <CardContent sx={{ p: 4 }}>
            <Box
              sx={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                mb: 3,
              }}
            >
              <Avatar
                sx={{
                  m: 1,
                  bgcolor: 'primary.main',
                  width: 56,
                  height: 56,
                }}
              >
                <SecurityIcon fontSize="large" />
              </Avatar>
              <Typography component="h1" variant="h4" color="primary" fontWeight="bold">
                Castellan
              </Typography>
              <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                Security Dashboard
              </Typography>
            </Box>

            <Form onSubmit={handleSubmit}>
              <Box sx={{ mt: 1 }}>
                <TextInput
                  source="username"
                  label="Username"
                  defaultValue="admin"
                  validate={required()}
                  fullWidth
                  disabled={loading}
                />
                <PasswordInput
                  source="password"
                  label="Password"
                  validate={required()}
                  fullWidth
                  disabled={loading}
                />
                <AdminButton
                  type="submit"
                  fullWidth
                  variant="contained"
                  disabled={loading}
                  sx={{ mt: 3, mb: 2, py: 1.5 }}
                >
                  {loading ? (
                    <>
                      <CircularProgress size={20} sx={{ mr: 1 }} />
                      Signing In...
                    </>
                  ) : (
                    'Sign In'
                  )}
                </AdminButton>
              </Box>
            </Form>

            <Box sx={{ mt: 3, p: 2, bgcolor: 'grey.50', borderRadius: 1 }}>
              <Typography variant="body2" color="text.secondary" align="center">
                <strong>Default Credentials:</strong>
              </Typography>
              <Typography variant="body2" color="text.secondary" align="center" sx={{ mt: 0.5 }}>
                Username: <code>admin</code>
              </Typography>
              <Typography variant="body2" color="text.secondary" align="center">
                Password: <code>CastellanAdmin2024!</code>
              </Typography>
            </Box>
          </CardContent>
        </Card>

        <Typography variant="body2" color="text.secondary" sx={{ mt: 3, textAlign: 'center' }}>
          Secure access to Castellan Security Platform
        </Typography>
      </Box>
    </Container>
  );
};
