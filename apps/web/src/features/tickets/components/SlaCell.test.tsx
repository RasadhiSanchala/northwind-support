import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaCell } from './SlaCell';

const states: { state: SlaStatus; label: string; tone: string }[] = [
  { state: 'NotApplicable', label: 'No SLA', tone: 'neutral' },
  { state: 'WithinSla', label: 'Within SLA', tone: 'info' },
  { state: 'AtRisk', label: 'At risk', tone: 'warning' },
  { state: 'Breached', label: 'Breached', tone: 'danger' },
  { state: 'Met', label: 'Met', tone: 'success' },
];

describe('SlaCell reusable indicator', () => {
  it.each(states)('distinguishes $state with text and style', ({ state, label, tone }) => {
    render(<SlaCell dueAtUtc={null} slaStatus={state} />);
    const badge = screen.getByText(label);
    expect(badge).toBeInTheDocument();
    expect(badge).toHaveClass(`badge--${tone}`);
  });
});
