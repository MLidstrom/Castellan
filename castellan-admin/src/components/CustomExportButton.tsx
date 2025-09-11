import React, { useState } from 'react';
import { Button, Tooltip } from '@mui/material';
import { Download as DownloadIcon } from '@mui/icons-material';
import { useListContext } from 'react-admin';
import ExportDialog from './ExportDialog';

const CustomExportButton: React.FC = () => {
  const [exportDialogOpen, setExportDialogOpen] = useState(false);
  const listContext = useListContext();
  
  // Get current filters from the list context
  const currentFilters = listContext?.filterValues || {};

  const handleExportClick = () => {
    setExportDialogOpen(true);
  };

  const handleExportDialogClose = () => {
    setExportDialogOpen(false);
  };

  return (
    <>
      <Tooltip title="Export filtered security events">
        <Button
          onClick={handleExportClick}
          startIcon={<DownloadIcon />}
          sx={{ 
            mr: 1,
            minWidth: 'auto',
            '& .MuiButton-startIcon': {
              mr: 0.5,
            }
          }}
        >
          Export
        </Button>
      </Tooltip>
      
      <ExportDialog
        open={exportDialogOpen}
        onClose={handleExportDialogClose}
        filters={currentFilters}
      />
    </>
  );
};

export default CustomExportButton;
