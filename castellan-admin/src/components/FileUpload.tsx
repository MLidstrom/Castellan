import React, { useState, useCallback } from 'react';
import {
  Box,
  Button,
  LinearProgress,
  Typography,
  Card,
  CardContent,
  IconButton,
  Chip,
  Alert,
  List,
  ListItem,
  ListItemText,
  ListItemSecondaryAction,
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions
} from '@mui/material';
import {
  CloudUpload as UploadIcon,
  Delete as DeleteIcon,
  InsertDriveFile as FileIcon,
  CheckCircle as SuccessIcon,
  Error as ErrorIcon,
  Close as CloseIcon
} from '@mui/icons-material';
import { useDataProvider, useNotify } from 'react-admin';

interface UploadFile {
  id: string;
  file: File;
  progress: number;
  status: 'pending' | 'uploading' | 'success' | 'error';
  error?: string;
  response?: any;
}

interface FileUploadProps {
  resource: string;
  accept?: string;
  maxSize?: number; // in MB
  maxFiles?: number;
  onUploadComplete?: (files: UploadFile[]) => void;
  onUploadError?: (error: string) => void;
}

export const FileUpload: React.FC<FileUploadProps> = ({
  resource,
  accept = '.pdf,.docx,.xlsx,.csv,.json,.txt',
  maxSize = 10, // 10MB default
  maxFiles = 5,
  onUploadComplete,
  onUploadError
}) => {
  const [files, setFiles] = useState<UploadFile[]>([]);
  const [dragOver, setDragOver] = useState(false);
  const [uploadDialogOpen, setUploadDialogOpen] = useState(false);
  const dataProvider = useDataProvider();
  const notify = useNotify();

  const generateId = () => Math.random().toString(36).substr(2, 9);

  const formatFileSize = (bytes: number) => {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  };

  const validateFile = (file: File): string | null => {
    // Check file size
    if (file.size > maxSize * 1024 * 1024) {
      return `File size exceeds ${maxSize}MB limit`;
    }

    // Check file type
    const fileExtension = '.' + file.name.split('.').pop()?.toLowerCase();
    const acceptedTypes = accept.split(',').map(type => type.trim().toLowerCase());
    
    if (!acceptedTypes.includes(fileExtension)) {
      return `File type ${fileExtension} is not supported`;
    }

    return null;
  };

  const addFiles = useCallback((newFiles: FileList | File[]) => {
    const fileArray = Array.from(newFiles);
    
    if (files.length + fileArray.length > maxFiles) {
      notify(`Cannot upload more than ${maxFiles} files`, { type: 'warning' });
      return;
    }

    const validFiles: UploadFile[] = [];
    
    fileArray.forEach(file => {
      const error = validateFile(file);
      validFiles.push({
        id: generateId(),
        file,
        progress: 0,
        status: error ? 'error' : 'pending',
        error: error || undefined
      });
    });

    setFiles(prev => [...prev, ...validFiles]);
    
    if (validFiles.some(f => f.status === 'pending')) {
      setUploadDialogOpen(true);
    }
  }, [files.length, maxFiles, notify, accept, maxSize]);

  const uploadFile = async (uploadFile: UploadFile): Promise<void> => {
    return new Promise((resolve, reject) => {
      const formData = new FormData();
      formData.append('file', uploadFile.file);
      formData.append('resource', resource);

      // Simulate progress for demo - in real implementation, use XMLHttpRequest or fetch with progress
      let progress = 0;
      const progressInterval = setInterval(() => {
        progress += Math.random() * 30;
        if (progress > 90) progress = 90;
        
        setFiles(prev => prev.map(f => 
          f.id === uploadFile.id 
            ? { ...f, progress, status: 'uploading' as const }
            : f
        ));
      }, 200);

      // Simulate upload delay
      setTimeout(async () => {
        clearInterval(progressInterval);
        
        try {
          // In real implementation, use dataProvider to upload file
          const response = await dataProvider.create(`${resource}/upload`, {
            data: { 
              filename: uploadFile.file.name,
              size: uploadFile.file.size,
              type: uploadFile.file.type,
              uploadedAt: new Date().toISOString()
            }
          });

          setFiles(prev => prev.map(f => 
            f.id === uploadFile.id 
              ? { ...f, progress: 100, status: 'success', response }
              : f
          ));
          
          notify(`File "${uploadFile.file.name}" uploaded successfully`, { type: 'success' });
          resolve();
          
        } catch (error) {
          const errorMessage = error instanceof Error ? error.message : 'Upload failed';
          setFiles(prev => prev.map(f => 
            f.id === uploadFile.id 
              ? { ...f, status: 'error', error: errorMessage }
              : f
          ));
          
          notify(`Upload failed: ${errorMessage}`, { type: 'error' });
          reject(error);
        }
      }, 2000);
    });
  };

  const uploadAll = async () => {
    const pendingFiles = files.filter(f => f.status === 'pending');
    
    try {
      await Promise.all(pendingFiles.map(uploadFile));
      onUploadComplete?.(files);
    } catch (error) {
      onUploadError?.(error instanceof Error ? error.message : 'Upload failed');
    }
  };

  const removeFile = (id: string) => {
    setFiles(prev => prev.filter(f => f.id !== id));
  };

  const clearAll = () => {
    setFiles([]);
    setUploadDialogOpen(false);
  };

  // Drag and drop handlers
  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragOver(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragOver(false);
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragOver(false);
    
    const droppedFiles = e.dataTransfer.files;
    if (droppedFiles.length > 0) {
      addFiles(droppedFiles);
    }
  }, [addFiles]);

  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      addFiles(e.target.files);
    }
  };

  return (
    <>
      {/* Upload Area */}
      <Card
        sx={{
          border: dragOver ? '2px dashed #1976d2' : '2px dashed #e0e0e0',
          backgroundColor: dragOver ? '#f5f5f5' : '#fafafa',
          cursor: 'pointer',
          transition: 'all 0.3s ease',
          '&:hover': {
            border: '2px dashed #1976d2',
            backgroundColor: '#f5f5f5'
          }
        }}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
      >
        <CardContent sx={{ textAlign: 'center', py: 4 }}>
          <UploadIcon sx={{ fontSize: 48, color: 'text.secondary', mb: 2 }} />
          <Typography variant="h6" gutterBottom>
            Drop files here or click to browse
          </Typography>
          <Typography variant="body2" color="textSecondary" paragraph>
            Accepted formats: {accept?.replace?.(/\./g, '')?.toUpperCase?.() || 'ALL'}
          </Typography>
          <Typography variant="body2" color="textSecondary" paragraph>
            Max file size: {maxSize}MB • Max files: {maxFiles}
          </Typography>
          
          <input
            type="file"
            multiple
            accept={accept}
            onChange={handleFileSelect}
            style={{ display: 'none' }}
            id="file-upload-input"
          />
          <label htmlFor="file-upload-input">
            <Button
              variant="contained"
              component="span"
              startIcon={<UploadIcon />}
              size="large"
            >
              Select Files
            </Button>
          </label>
        </CardContent>
      </Card>

      {/* File List */}
      {files.length > 0 && (
        <Card sx={{ mt: 2 }}>
          <CardContent>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 2 }}>
              <Typography variant="h6">
                Files ({files.length})
              </Typography>
              <Button
                onClick={() => setUploadDialogOpen(true)}
                variant="outlined"
                size="small"
              >
                Manage Uploads
              </Button>
            </Box>
            
            <List dense>
              {files.slice(0, 3).map((file) => (
                <ListItem key={file.id}>
                  <FileIcon sx={{ mr: 1, color: 'text.secondary' }} />
                  <ListItemText
                    primary={file.file.name}
                    secondary={`${formatFileSize(file.file.size)} • ${file.status}`}
                  />
                  <ListItemSecondaryAction>
                    <Chip
                      size="small"
                      icon={
                        file.status === 'success' ? <SuccessIcon /> :
                        file.status === 'error' ? <ErrorIcon /> : undefined
                      }
                      label={file.status}
                      color={
                        file.status === 'success' ? 'success' :
                        file.status === 'error' ? 'error' : 'default'
                      }
                    />
                    <IconButton
                      size="small"
                      onClick={() => removeFile(file.id)}
                      sx={{ ml: 1 }}
                    >
                      <DeleteIcon fontSize="small" />
                    </IconButton>
                  </ListItemSecondaryAction>
                </ListItem>
              ))}
            </List>
            
            {files.length > 3 && (
              <Typography variant="body2" color="textSecondary" sx={{ mt: 1 }}>
                +{files.length - 3} more files
              </Typography>
            )}
          </CardContent>
        </Card>
      )}

      {/* Upload Management Dialog */}
      <Dialog
        open={uploadDialogOpen}
        onClose={() => setUploadDialogOpen(false)}
        maxWidth="md"
        fullWidth
      >
        <DialogTitle>
          <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            Upload Manager
            <IconButton onClick={() => setUploadDialogOpen(false)}>
              <CloseIcon />
            </IconButton>
          </Box>
        </DialogTitle>
        
        <DialogContent>
          {files.some(f => f.status === 'error') && (
            <Alert severity="warning" sx={{ mb: 2 }}>
              Some files have errors. Please review and fix before uploading.
            </Alert>
          )}
          
          <List>
            {files.map((file) => (
              <ListItem key={file.id} sx={{ flexDirection: 'column', alignItems: 'stretch' }}>
                <Box sx={{ display: 'flex', alignItems: 'center', width: '100%', mb: 1 }}>
                  <FileIcon sx={{ mr: 1 }} />
                  <Box sx={{ flexGrow: 1 }}>
                    <Typography variant="body1">{file.file.name}</Typography>
                    <Typography variant="body2" color="textSecondary">
                      {formatFileSize(file.file.size)}
                    </Typography>
                  </Box>
                  <Chip
                    size="small"
                    icon={
                      file.status === 'success' ? <SuccessIcon /> :
                      file.status === 'error' ? <ErrorIcon /> : undefined
                    }
                    label={file.status}
                    color={
                      file.status === 'success' ? 'success' :
                      file.status === 'error' ? 'error' : 'default'
                    }
                  />
                  <IconButton
                    size="small"
                    onClick={() => removeFile(file.id)}
                    sx={{ ml: 1 }}
                  >
                    <DeleteIcon fontSize="small" />
                  </IconButton>
                </Box>
                
                {file.status === 'uploading' && (
                  <LinearProgress
                    variant="determinate"
                    value={file.progress}
                    sx={{ mb: 1 }}
                  />
                )}
                
                {file.error && (
                  <Alert severity="error" sx={{ mb: 1 }}>
                    {file.error}
                  </Alert>
                )}
              </ListItem>
            ))}
          </List>
        </DialogContent>
        
        <DialogActions>
          <Button onClick={clearAll}>Clear All</Button>
          <Button
            onClick={uploadAll}
            variant="contained"
            disabled={!files.some(f => f.status === 'pending')}
            startIcon={<UploadIcon />}
          >
            Upload All ({files.filter(f => f.status === 'pending').length})
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
};