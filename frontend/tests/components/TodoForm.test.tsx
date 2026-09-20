import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { TodoForm } from '../../src/components/TodoForm'

const renderForm = (props: Partial<React.ComponentProps<typeof TodoForm>> = {}) => {
  const action = vi.fn().mockResolvedValue(null)
  const router = createMemoryRouter(
    [{ path: '/', element: <TodoForm intent="add" {...props} />, action }],
    { initialEntries: ['/'] },
  )
  render(<RouterProvider router={router} />)
  return { action }
}

describe('TodoForm', () => {
  it('renders title, description, due date fields and a hidden intent field', () => {
    renderForm()

    expect(screen.getByLabelText('Title')).toBeInTheDocument()
    expect(screen.getByLabelText('Description')).toBeInTheDocument()
    expect(screen.getByLabelText('Due date')).toBeInTheDocument()
    expect(document.querySelector('input[name="intent"][value="add"]')).toBeInTheDocument()
  })

  it('lets the due date be picked as a date and time, not just a date', () => {
    renderForm()

    expect(screen.getByLabelText('Due date')).toHaveAttribute('type', 'datetime-local')
  })

  it('shows a passed-in field error next to the matching field', () => {
    renderForm({ fieldErrors: { Title: ["'Title' must not be empty."] } })

    expect(screen.getByText("'Title' must not be empty.")).toBeInTheDocument()
  })

  it('blocks submit on an empty title without hitting the action', async () => {
    const user = userEvent.setup()
    const { action } = renderForm()

    await user.click(screen.getByRole('button', { name: /add to-do/i }))

    expect(screen.getByText('Title is required.')).toBeInTheDocument()
    expect(action).not.toHaveBeenCalled()
  })

  it('submits when a title is present', async () => {
    const user = userEvent.setup()
    const { action } = renderForm()

    await user.type(screen.getByLabelText('Title'), 'Buy milk')
    await user.click(screen.getByRole('button', { name: /add to-do/i }))

    expect(action).toHaveBeenCalled()
  })

  it('disables the submit button while submitting', () => {
    renderForm({ submitting: true })

    expect(screen.getByRole('button', { name: /adding/i })).toBeDisabled()
  })
})
