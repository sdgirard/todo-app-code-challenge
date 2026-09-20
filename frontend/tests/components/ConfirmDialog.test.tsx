import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ConfirmDialog } from '../../src/components/ConfirmDialog'

describe('ConfirmDialog', () => {
  it('renders nothing when closed', () => {
    const { container } = render(
      <ConfirmDialog open={false} message="Delete this?" onConfirm={vi.fn()} onCancel={vi.fn()} />,
    )

    expect(container).toBeEmptyDOMElement()
  })

  it('renders the message and both buttons when open', () => {
    render(<ConfirmDialog open message="Delete this to-do?" onConfirm={vi.fn()} onCancel={vi.fn()} />)

    expect(screen.getByText('Delete this to-do?')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Yes' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'No' })).toBeInTheDocument()
  })

  it('calls onConfirm when Yes is clicked', async () => {
    const user = userEvent.setup()
    const onConfirm = vi.fn()
    const onCancel = vi.fn()

    render(<ConfirmDialog open message="Delete this?" onConfirm={onConfirm} onCancel={onCancel} />)
    await user.click(screen.getByRole('button', { name: 'Yes' }))

    expect(onConfirm).toHaveBeenCalledOnce()
    expect(onCancel).not.toHaveBeenCalled()
  })

  it('calls onCancel when No is clicked', async () => {
    const user = userEvent.setup()
    const onConfirm = vi.fn()
    const onCancel = vi.fn()

    render(<ConfirmDialog open message="Delete this?" onConfirm={onConfirm} onCancel={onCancel} />)
    await user.click(screen.getByRole('button', { name: 'No' }))

    expect(onCancel).toHaveBeenCalledOnce()
    expect(onConfirm).not.toHaveBeenCalled()
  })
})
