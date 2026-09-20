import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import App from '../src/App'

describe('App', () => {
  it('renders an empty state with no to-dos', () => {
    render(<App />)

    expect(screen.getByText('No to-dos yet.')).toBeInTheDocument()
  })

  it('adds a to-do when the form is submitted', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByLabelText('New to-do title'), 'Buy milk')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    expect(screen.getByText('Buy milk')).toBeInTheDocument()
    expect(screen.queryByText('No to-dos yet.')).not.toBeInTheDocument()
  })

  it('toggles a to-do as completed', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByLabelText('New to-do title'), 'Buy milk')
    await user.click(screen.getByRole('button', { name: 'Add' }))

    const checkbox = screen.getByRole('checkbox', {
      name: 'Mark "Buy milk" as complete',
    })
    await user.click(checkbox)

    expect(checkbox).toBeChecked()
    expect(screen.getByText('Buy milk')).toHaveClass('line-through')
  })

  it('deletes a to-do', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.type(screen.getByLabelText('New to-do title'), 'Buy milk')
    await user.click(screen.getByRole('button', { name: 'Add' }))
    await user.click(screen.getByRole('button', { name: 'Delete "Buy milk"' }))

    expect(screen.getByText('No to-dos yet.')).toBeInTheDocument()
  })
})
