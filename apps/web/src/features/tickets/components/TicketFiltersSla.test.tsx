import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { emptyFilters } from '../hooks/useTicketList';
import { TicketFilters } from './TicketFilters';

describe('TicketFilters', () => {
  it('reports the selected SQL-backed SLA filter', () => {
    const onChange = vi.fn();
    render(
      <TicketFilters
        filters={emptyFilters}
        agents={[]}
        categories={[]}
        customers={[]}
        onChange={onChange}
        onReset={vi.fn()}
      />,
    );

    fireEvent.change(screen.getByLabelText('SLA status'), { target: { value: 'AtRisk' } });
    expect(onChange).toHaveBeenCalledWith({ slaStatus: 'AtRisk' });
  });
});
