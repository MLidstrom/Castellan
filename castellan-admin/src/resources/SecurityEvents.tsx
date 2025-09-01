import React from 'react';
import {
  List,
  Datagrid,
  TextField,
  DateField,
  NumberField,
  EditButton,
  ShowButton,
  Edit,
  Create,
  Show,
  SimpleForm,
  SimpleShowLayout,
  TextInput,
  SelectInput,
  required,
  // ChipField, // Removed - using custom component
  Filter,
  useRecordContext,
  // ReferenceField, // Not used
} from 'react-admin';
import { Chip, Box, Typography, Tooltip } from '@mui/material';
import { Security as SecurityIcon } from '@mui/icons-material';

// MITRE ATT&CK technique descriptions mapping
const MITRE_TECHNIQUE_DESCRIPTIONS: { [key: string]: string } = {
  'T1003': 'OS Credential Dumping - Adversaries attempt to dump credentials to obtain account login and credential material',
  'T1005': 'Data from Local System - Adversaries may search local system sources to find files of interest',
  'T1012': 'Query Registry - Adversaries may interact with the Windows Registry to gather information about the system',
  'T1016': 'System Network Configuration Discovery - Adversaries may look for details about the network configuration and settings',
  'T1018': 'Remote System Discovery - Adversaries may attempt to get a listing of other systems by IP address, hostname, or other logical identifier',
  'T1033': 'System Owner/User Discovery - Adversaries may attempt to identify the primary user, currently logged in user, set of users',
  'T1041': 'Exfiltration Over C2 Channel - Adversaries may steal data by exfiltrating it over an existing command and control channel',
  'T1047': 'Windows Management Instrumentation - Adversaries may abuse Windows Management Instrumentation (WMI) to execute malicious commands',
  'T1049': 'System Network Connections Discovery - Adversaries may attempt to get a listing of network connections to or from the compromised system',
  'T1053': 'Scheduled Task/Job - Adversaries may abuse task scheduling functionality to facilitate initial or recurring execution of malicious code',
  'T1055': 'Process Injection - Adversaries may inject code into processes to evade process-based defenses',
  'T1057': 'Process Discovery - Adversaries may attempt to get information about running processes on a system',
  'T1059': 'Command and Scripting Interpreter - Adversaries may abuse command and script interpreters to execute commands',
  'T1068': 'Exploitation for Privilege Escalation - Adversaries may exploit software vulnerabilities in an attempt to elevate privileges',
  'T1070': 'Indicator Removal on Host - Adversaries may delete or modify artifacts generated within systems to remove evidence',
  'T1071': 'Application Layer Protocol - Adversaries may communicate using application layer protocols to avoid detection',
  'T1078': 'Valid Accounts - Adversaries may obtain and abuse credentials of existing accounts as a means of gaining Initial Access',
  'T1082': 'System Information Discovery - Adversaries may attempt to get detailed information about the operating system and hardware',
  'T1083': 'File and Directory Discovery - Adversaries may enumerate files and directories or may search in specific locations',
  'T1087': 'Account Discovery - Adversaries may attempt to get a listing of accounts on a system or within an environment',
  'T1090': 'Proxy - Adversaries may use a connection proxy to direct network traffic between systems or act as an intermediary',
  'T1105': 'Ingress Tool Transfer - Adversaries may transfer tools or other files from an external system into a compromised environment',
  'T1112': 'Modify Registry - Adversaries may interact with the Windows Registry to hide configuration information within Registry keys',
  'T1134': 'Access Token Manipulation - Adversaries may modify access tokens to operate under a different user or system security context',
  'T1135': 'Network Share Discovery - Adversaries may look for folders and drives shared on remote systems as a means of identifying sources of information',
  'T1140': 'Deobfuscate/Decode Files or Information - Adversaries may use obfuscated files or information to hide artifacts of an intrusion',
  'T1190': 'Exploit Public-Facing Application - Adversaries may attempt to exploit a weakness in an Internet-facing computer or program',
  'T1482': 'Domain Trust Discovery - Adversaries may attempt to gather information on domain trust relationships',
  'T1484': 'Domain Policy Modification - Adversaries may modify the configuration settings of a domain to evade defenses',
  'T1518': 'Software Discovery - Adversaries may attempt to get a listing of software and software versions',
  'T1543': 'Create or Modify System Process - Adversaries may create or modify system-level processes to repeatedly execute malicious payloads',
  'T1547': 'Boot or Logon Autostart Execution - Adversaries may configure system settings to automatically execute a program during system boot',
  'T1548': 'Abuse Elevation Control Mechanism - Adversaries may circumvent mechanisms designed to control elevate privileges',
  'T1550': 'Use Alternate Authentication Material - Adversaries may use alternate authentication material, such as password hashes',
  'T1552': 'Unsecured Credentials - Adversaries may search compromised systems to find and obtain insecurely stored credentials',
  'T1555': 'Credentials from Password Stores - Adversaries may search for common password storage locations to obtain user credentials',
  'T1562': 'Impair Defenses - Adversaries may maliciously modify components of a victim environment to hinder or disable defensive mechanisms',
  'T1564': 'Hide Artifacts - Adversaries may attempt to hide artifacts associated with their behaviors to evade detection',
  'T1566': 'Phishing - Adversaries may send phishing messages to gain access to victim systems',
  'T1569': 'System Services - Adversaries may abuse system services or daemons to execute commands or programs',
  'T1574': 'Hijack Execution Flow - Adversaries may execute their own malicious payloads by hijacking the way operating systems run programs',
  'T1588': 'Obtain Capabilities - Adversaries may buy and/or steal capabilities that can be used during targeting',
  'T1595': 'Active Scanning - Adversaries may execute active reconnaissance scans to gather information that can be used during targeting'
};

// Custom header component with shield icon
const SecurityEventsHeader = () => (
  <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
    <SecurityIcon sx={{ mr: 1, color: 'primary.main' }} />
    <Typography variant="h4" component="h1">
      Security Events
    </Typography>
  </Box>
);

// Custom component for risk level with color coding
const RiskLevelField = ({ source }: any) => {
  const record = useRecordContext();
  
  const getRiskColor = (level: string) => {
    switch (level?.toLowerCase()) {
      case 'critical': return 'error';
      case 'high': return 'warning';
      case 'medium': return 'info';
      case 'low': return 'success';
      default: return 'default';
    }
  };

  // Get the value from the record using the source field
  const riskLevel = record?.[source] || record?.riskLevel || record?.RiskLevel || 'Unknown';

  return (
    <Chip 
      label={riskLevel} 
      color={getRiskColor(riskLevel)}
      size="small"
    />
  );
};

// Custom component for MITRE ATT&CK techniques with tooltips
const MitreTechniquesField = ({ source }: any) => {
  const record = useRecordContext();
  const techniques = record?.[source] || record?.mitreAttack || record?.MitreAttack || [];
  
  // Helper function to get description for a technique
  const getTechniqueDescription = (technique: string): string => {
    const cleanTechnique = technique.trim().toUpperCase();
    return MITRE_TECHNIQUE_DESCRIPTIONS[cleanTechnique] || `MITRE ATT&CK Technique: ${cleanTechnique}`;
  };
  
  return (
    <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
      {techniques.slice(0, 3).map((technique: string, index: number) => (
        <Tooltip
          key={index}
          title={getTechniqueDescription(technique)}
          arrow
          placement="top"
        >
          <Chip 
            label={technique.trim()}
            variant="outlined"
            size="small"
          />
        </Tooltip>
      ))}
      {techniques.length > 3 && (
        <Tooltip
          title={`Additional techniques: ${techniques.slice(3).map((t: string) => t.trim()).join(', ')}`}
          arrow
          placement="top"
        >
          <Chip 
            label={`+${techniques.length - 3} more`}
            variant="outlined"
            size="small"
            color="primary"
          />
        </Tooltip>
      )}
    </Box>
  );
};

// Filters for the list view
const SecurityEventFilters = [
  <TextInput source="eventType" label="Event Type" alwaysOn />,
  <SelectInput 
    source="riskLevel" 
    label="Risk Level"
    choices={[
      { id: 'low', name: 'Low' },
      { id: 'medium', name: 'Medium' },
      { id: 'high', name: 'High' },
      { id: 'critical', name: 'Critical' },
    ]}
    alwaysOn
  />,
  <TextInput source="machine" label="Machine" />,
  <TextInput source="user" label="User" />,
  <TextInput source="source" label="Source" />,
];

export const SecurityEventList = () => (
  <Box>
    <SecurityEventsHeader />
    <List 
      filters={<Filter>{SecurityEventFilters}</Filter>}
      sort={{ field: 'timestamp', order: 'DESC' }}
      perPage={25}
      title=" "
    >
      <Datagrid rowClick="show" size="small">
      <TextField source="id" />
      <TextField source="eventType" sortable={false} />
      <RiskLevelField source="riskLevel" label="Risk Level" />
      <NumberField source="correlationScore" options={{ minimumFractionDigits: 2, maximumFractionDigits: 2 }} />
      <NumberField source="confidence" options={{ minimumFractionDigits: 0, maximumFractionDigits: 0 }} />
      <TextField source="machine" sortable={false} label="Machine" />
      <TextField source="user" sortable={false} />
      <MitreTechniquesField source="mitreAttack" label="MITRE Techniques" sortable={false} />
      <TextField source="source" sortable={false} label="Source" />
      <DateField source="timestamp" showTime />
      <ShowButton />
      <EditButton />
    </Datagrid>
    </List>
  </Box>
);

export const SecurityEventShow = () => (
  <Box>
    <SecurityEventsHeader />
    <Show title=" ">
      <SimpleShowLayout>
      <TextField source="id" />
      <TextField source="eventId" />
      <TextField source="eventType" />
      <RiskLevelField source="riskLevel" label="Risk Level" />
      <NumberField source="correlationScore" options={{ minimumFractionDigits: 2, maximumFractionDigits: 2 }} />
      <NumberField source="confidence" options={{ minimumFractionDigits: 0, maximumFractionDigits: 0 }} />
      <TextField source="ipAddresses" label="IP Addresses" />
      <TextField source="machine" />
      <TextField source="user" />
      <TextField source="mitreAttack" label="MITRE Techniques" />
      <TextField source="source" label="Source" />
      <TextField source="message" label="Message" />
      <TextField source="recommendedActions" />
      <TextField source="enrichedIPs" label="IP Enrichment" />
      <DateField source="timestamp" showTime />
    </SimpleShowLayout>
    </Show>
  </Box>
);

export const SecurityEventEdit = () => (
  <Box>
    <SecurityEventsHeader />
    <Edit title=" ">
      <SimpleForm>
      <TextInput disabled source="id" />
      <TextInput disabled source="eventId" />
      <TextInput source="eventType" validate={required()} />
      <SelectInput 
        source="riskLevel" 
        validate={required()}
        choices={[
          { id: 'low', name: 'Low' },
          { id: 'medium', name: 'Medium' },
          { id: 'high', name: 'High' },
          { id: 'critical', name: 'Critical' },
        ]}
      />
      <TextInput disabled source="correlationScore" />
      <TextInput disabled source="confidence" />
      <TextInput disabled source="ipAddresses" label="IP Addresses" />
      <TextInput disabled source="machine" />
      <TextInput disabled source="user" />
      <TextInput disabled source="mitreAttack" label="MITRE Techniques" />
      <TextInput disabled source="source" label="Source" />
      <TextInput 
        source="message" 
        label="Message"
        multiline
        rows={3}
        sx={{ width: '100%' }}
      />
      <TextInput 
        source="recommendedActions" 
        multiline
        rows={3}
        sx={{ width: '100%' }}
      />
      <TextInput multiline rows={2} source="notes" label="Investigation Notes" />
    </SimpleForm>
    </Edit>
  </Box>
);

export const SecurityEventCreate = () => (
  <Box>
    <SecurityEventsHeader />
    <Create title=" ">
      <SimpleForm>
      <TextInput source="eventType" validate={required()} />
      <SelectInput 
        source="riskLevel" 
        validate={required()}
        choices={[
          { id: 'low', name: 'Low' },
          { id: 'medium', name: 'Medium' },
          { id: 'high', name: 'High' },
          { id: 'critical', name: 'Critical' },
        ]}
      />
      <TextInput source="correlationScore" validate={required()} />
      <TextInput source="confidence" validate={required()} />
      <TextInput source="machine" />
      <TextInput source="user" />
      <TextInput source="mitreAttack" label="MITRE Techniques" />
      <TextInput source="source" label="Source" validate={required()} />
      <TextInput 
        source="message" 
        label="Message"
        multiline
        rows={3}
        validate={required()}
        sx={{ width: '100%' }}
      />
      <TextInput 
        source="recommendedActions" 
        multiline
        rows={3}
        sx={{ width: '100%' }}
      />
    </SimpleForm>
    </Create>
  </Box>
);