import React, { useState, useMemo } from 'react';
import {
  Box,
  TextField,
  Chip,
  Typography,
  Paper,
  Grid,
  Button,
  Collapse,
  IconButton
} from '@mui/material';
import {
  Search as SearchIcon,
  ExpandMore as ExpandMoreIcon,
  ExpandLess as ExpandLessIcon,
  Clear as ClearIcon
} from '@mui/icons-material';

// Common MITRE ATT&CK techniques for security events
const MITRE_TECHNIQUES = [
  // Initial Access
  { id: 'T1078', name: 'Valid Accounts', tactic: 'Initial Access' },
  { id: 'T1190', name: 'Exploit Public-Facing Application', tactic: 'Initial Access' },
  { id: 'T1566', name: 'Phishing', tactic: 'Initial Access' },
  
  // Execution
  { id: 'T1059', name: 'Command and Scripting Interpreter', tactic: 'Execution' },
  { id: 'T1106', name: 'Native API', tactic: 'Execution' },
  { id: 'T1053', name: 'Scheduled Task/Job', tactic: 'Execution' },
  
  // Persistence
  { id: 'T1547', name: 'Boot or Logon Autostart Execution', tactic: 'Persistence' },
  { id: 'T1543', name: 'Create or Modify System Process', tactic: 'Persistence' },
  { id: 'T1136', name: 'Create Account', tactic: 'Persistence' },
  
  // Privilege Escalation
  { id: 'T1548', name: 'Abuse Elevation Control Mechanism', tactic: 'Privilege Escalation' },
  { id: 'T1068', name: 'Exploitation for Privilege Escalation', tactic: 'Privilege Escalation' },
  { id: 'T1055', name: 'Process Injection', tactic: 'Privilege Escalation' },
  
  // Defense Evasion
  { id: 'T1562', name: 'Impair Defenses', tactic: 'Defense Evasion' },
  { id: 'T1070', name: 'Indicator Removal', tactic: 'Defense Evasion' },
  { id: 'T1027', name: 'Obfuscated Files or Information', tactic: 'Defense Evasion' },
  
  // Credential Access
  { id: 'T1110', name: 'Brute Force', tactic: 'Credential Access' },
  { id: 'T1003', name: 'OS Credential Dumping', tactic: 'Credential Access' },
  { id: 'T1558', name: 'Steal or Forge Kerberos Tickets', tactic: 'Credential Access' },
  
  // Discovery
  { id: 'T1057', name: 'Process Discovery', tactic: 'Discovery' },
  { id: 'T1018', name: 'Remote System Discovery', tactic: 'Discovery' },
  { id: 'T1033', name: 'System Owner/User Discovery', tactic: 'Discovery' },
  
  // Lateral Movement
  { id: 'T1021', name: 'Remote Services', tactic: 'Lateral Movement' },
  { id: 'T1550', name: 'Use Alternate Authentication Material', tactic: 'Lateral Movement' },
  
  // Collection
  { id: 'T1005', name: 'Data from Local System', tactic: 'Collection' },
  { id: 'T1039', name: 'Data from Network Shared Drive', tactic: 'Collection' },
  
  // Exfiltration
  { id: 'T1041', name: 'Exfiltration Over C2 Channel', tactic: 'Exfiltration' },
  { id: 'T1048', name: 'Exfiltration Over Alternative Protocol', tactic: 'Exfiltration' },
  
  // Impact
  { id: 'T1486', name: 'Data Encrypted for Impact', tactic: 'Impact' },
  { id: 'T1490', name: 'Inhibit System Recovery', tactic: 'Impact' }
];

export interface MitreTechniqueFilterProps {
  selectedTechniques: string[];
  onChange: (techniques: string[]) => void;
  disabled?: boolean;
}

export const MitreTechniqueFilter: React.FC<MitreTechniqueFilterProps> = ({
  selectedTechniques,
  onChange,
  disabled = false
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [expandedTactics, setExpandedTactics] = useState<Set<string>>(new Set());

  const tacticsGroups = useMemo(() => {
    const groups = MITRE_TECHNIQUES.reduce((acc, technique) => {
      if (!acc[technique.tactic]) {
        acc[technique.tactic] = [];
      }
      acc[technique.tactic].push(technique);
      return acc;
    }, {} as Record<string, typeof MITRE_TECHNIQUES>);

    // Sort tactics alphabetically
    return Object.keys(groups)
      .sort()
      .reduce((acc, tactic) => {
        acc[tactic] = groups[tactic].sort((a, b) => a.name.localeCompare(b.name));
        return acc;
      }, {} as Record<string, typeof MITRE_TECHNIQUES>);
  }, []);

  const filteredTechniques = useMemo(() => {
    if (!searchTerm) return MITRE_TECHNIQUES;
    const term = searchTerm.toLowerCase();
    return MITRE_TECHNIQUES.filter(
      technique => 
        technique.id.toLowerCase().includes(term) ||
        technique.name.toLowerCase().includes(term) ||
        technique.tactic.toLowerCase().includes(term)
    );
  }, [searchTerm]);

  const handleTechniqueToggle = (techniqueId: string) => {
    const newSelection = selectedTechniques.includes(techniqueId)
      ? selectedTechniques.filter(id => id !== techniqueId)
      : [...selectedTechniques, techniqueId];
    onChange(newSelection);
  };

  const handleTacticToggle = (tactic: string) => {
    setExpandedTactics(prev => {
      const newSet = new Set(prev);
      if (newSet.has(tactic)) {
        newSet.delete(tactic);
      } else {
        newSet.add(tactic);
      }
      return newSet;
    });
  };

  const handleSelectAllInTactic = (tactic: string) => {
    const tacticTechniques = tacticsGroups[tactic].map(t => t.id);
    const newSelection = Array.from(new Set([...selectedTechniques, ...tacticTechniques]));
    onChange(newSelection);
  };

  const handleClearAllInTactic = (tactic: string) => {
    const tacticTechniques = tacticsGroups[tactic].map(t => t.id);
    const newSelection = selectedTechniques.filter(id => !tacticTechniques.includes(id));
    onChange(newSelection);
  };

  const getTechniqueName = (id: string): string => {
    const technique = MITRE_TECHNIQUES.find(t => t.id === id);
    return technique ? `${technique.id} - ${technique.name}` : id;
  };

  return (
    <Box>
      {/* Search Input */}
      <TextField
        fullWidth
        size="small"
        placeholder="Search techniques by ID, name, or tactic..."
        value={searchTerm}
        onChange={(e) => setSearchTerm(e.target.value)}
        disabled={disabled}
        InputProps={{
          startAdornment: <SearchIcon color="action" sx={{ mr: 1 }} />
        }}
        sx={{
          mb: 2,
          '& .MuiOutlinedInput-root': {
            backgroundColor: 'background.paper'
          }
        }}
      />

      {/* Selected Techniques Display */}
      {selectedTechniques.length > 0 && (
        <Box sx={{ mb: 2 }}>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Selected ({selectedTechniques.length}):
          </Typography>
          <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
            {selectedTechniques.map(id => (
              <Chip
                key={id}
                label={getTechniqueName(id)}
                size="small"
                onDelete={() => handleTechniqueToggle(id)}
                color="primary"
                sx={{ fontSize: '0.75rem' }}
              />
            ))}
          </Box>
          <Button
            size="small"
            startIcon={<ClearIcon />}
            onClick={() => onChange([])}
            color="error"
            sx={{ mt: 1, fontSize: '0.75rem' }}
          >
            Clear All
          </Button>
        </Box>
      )}

      {/* Techniques List */}
      {searchTerm ? (
        // Filtered search results
        <Box>
          <Typography variant="body2" sx={{ mb: 1, fontWeight: 'medium' }}>
            Search Results ({filteredTechniques.length}):
          </Typography>
          {filteredTechniques.map(technique => (
            <Box key={technique.id} sx={{ mb: 0.5 }}>
              <Chip
                label={`${technique.id} - ${technique.name}`}
                size="small"
                clickable
                onClick={() => handleTechniqueToggle(technique.id)}
                color={selectedTechniques.includes(technique.id) ? "primary" : "default"}
                variant={selectedTechniques.includes(technique.id) ? "filled" : "outlined"}
                sx={{ 
                  fontSize: '0.75rem',
                  maxWidth: '100%',
                  '& .MuiChip-label': {
                    display: 'block',
                    whiteSpace: 'nowrap',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis'
                  }
                }}
              />
            </Box>
          ))}
        </Box>
      ) : (
        // Grouped by tactics
        <Box>
          {Object.entries(tacticsGroups).map(([tactic, techniques]) => (
            <Paper key={tactic} variant="outlined" sx={{ mb: 1 }}>
              <Box
                sx={{
                  p: 1,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  cursor: 'pointer',
                  backgroundColor: 'action.hover'
                }}
                onClick={() => handleTacticToggle(tactic)}
              >
                <Typography variant="body2" sx={{ fontWeight: 'medium' }}>
                  {tactic} ({techniques.length})
                </Typography>
                <IconButton size="small">
                  {expandedTactics.has(tactic) ? <ExpandLessIcon /> : <ExpandMoreIcon />}
                </IconButton>
              </Box>
              
              <Collapse in={expandedTactics.has(tactic)}>
                <Box sx={{ p: 1, pt: 0 }}>
                  <Box sx={{ mb: 1, display: 'flex', gap: 0.5 }}>
                    <Button
                      size="small"
                      onClick={() => handleSelectAllInTactic(tactic)}
                      disabled={disabled}
                      sx={{ fontSize: '0.7rem' }}
                    >
                      Select All
                    </Button>
                    <Button
                      size="small"
                      onClick={() => handleClearAllInTactic(tactic)}
                      disabled={disabled}
                      color="error"
                      sx={{ fontSize: '0.7rem' }}
                    >
                      Clear All
                    </Button>
                  </Box>
                  
                  <Grid container spacing={0.5}>
                    {techniques.map(technique => (
                      <Grid item xs={12} key={technique.id}>
                        <Chip
                          label={`${technique.id} - ${technique.name}`}
                          size="small"
                          clickable
                          onClick={() => handleTechniqueToggle(technique.id)}
                          color={selectedTechniques.includes(technique.id) ? "primary" : "default"}
                          variant={selectedTechniques.includes(technique.id) ? "filled" : "outlined"}
                          sx={{ 
                            width: '100%',
                            fontSize: '0.7rem',
                            justifyContent: 'flex-start',
                            '& .MuiChip-label': {
                              display: 'block',
                              whiteSpace: 'nowrap',
                              overflow: 'hidden',
                              textOverflow: 'ellipsis'
                            }
                          }}
                        />
                      </Grid>
                    ))}
                  </Grid>
                </Box>
              </Collapse>
            </Paper>
          ))}
        </Box>
      )}
    </Box>
  );
};
