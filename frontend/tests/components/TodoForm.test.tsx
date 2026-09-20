import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { TodoForm } from '../../src/components/TodoForm'
import type { TodoResponse } from '../../src/api/generated/models'

const renderForm = (props: Partial<React.ComponentProps<typeof TodoForm>> = {}) => {
  const action = vi.fn().mockResolvedValue(null)
  const router = createMemoryRouter(
    [{ path: '/', element: <TodoForm intent="add" {...props} />, action }],
    { initialEntries: ['/'] },
  )
  render(<RouterProvider router={router} />)
  return { action }
}

const editTodo: TodoResponse = {
  id: '3fa85f64-5717-4562-b3fc-2c963f66afa6',
  title: 'Buy milk',
  description: '2% or whole, whichever is on sale',
  dueDate: '2026-09-25T14:30:00Z',
  isCompleted: false,
  createdAt: '2026-09-19T14:32:07.1234567Z',
  updatedAt: null,
}

const renderEditForm = (props: Partial<React.ComponentProps<typeof TodoForm>> = {}) => {
  const action = vi.fn().mockResolvedValue(null)
  const onCancel = vi.fn()
  const router = createMemoryRouter(
    [
      {
        path: '/',
        element: <TodoForm intent="edit" todo={editTodo} onCancel={onCancel} {...props} />,
        action,
      },
    ],
    { initialEntries: ['/'] },
  )
  render(<RouterProvider router={router} />)
  return { action, onCancel }
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

describe('TodoForm (edit mode)', () => {
  it('pre-fills all three fields from the todo prop', () => {
    renderEditForm()

    const expectedDueDate = new Date(editTodo.dueDate!)
    const pad = (n: number) => String(n).padStart(2, '0')
    const expectedLocalValue = `${expectedDueDate.getFullYear()}-${pad(expectedDueDate.getMonth() + 1)}-${pad(expectedDueDate.getDate())}T${pad(expectedDueDate.getHours())}:${pad(expectedDueDate.getMinutes())}`

    expect(screen.getByLabelText('Title')).toHaveValue(editTodo.title)
    expect(screen.getByLabelText('Description')).toHaveValue(editTodo.description)
    expect(screen.getByLabelText('Due date')).toHaveValue(expectedLocalValue)
  })

  it('starts with Save disabled since no field has changed yet', () => {
    renderEditForm()

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('enables Save when title changes, and disables it again when reverted', async () => {
    const user = userEvent.setup()
    renderEditForm()

    const titleInput = screen.getByLabelText('Title')
    await user.type(titleInput, '!')
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled()

    await user.type(titleInput, '{backspace}')
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('does not enable Save when description changes from a non-empty value to empty and back', async () => {
    const user = userEvent.setup()
    renderEditForm()

    const descriptionInput = screen.getByLabelText('Description')
    await user.clear(descriptionInput)
    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled()

    await user.type(descriptionInput, editTodo.description!)
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('does not enable Save when a null original description is edited to an empty string', () => {
    renderEditForm({ todo: { ...editTodo, description: null } })

    expect(screen.getByLabelText('Description')).toHaveValue('')
    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('enables Save when the due date changes, and keeps it disabled when untouched', async () => {
    const user = userEvent.setup()
    renderEditForm()

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()

    const dueDateInput = screen.getByLabelText('Due date')
    await user.clear(dueDateInput)
    await user.type(dueDateInput, '2026-10-01T09:00')

    expect(screen.getByRole('button', { name: 'Save' })).toBeEnabled()
  })

  it('keeps Save disabled when title is dirty but invalid (empty)', async () => {
    const user = userEvent.setup()
    renderEditForm()

    await user.clear(screen.getByLabelText('Title'))

    expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled()
  })

  it('calls onCancel and submits nothing when Cancel is clicked', async () => {
    const user = userEvent.setup()
    const { action, onCancel } = renderEditForm()

    await user.type(screen.getByLabelText('Title'), '!')
    await user.click(screen.getByRole('button', { name: 'Cancel' }))

    expect(onCancel).toHaveBeenCalled()
    expect(action).not.toHaveBeenCalled()
  })

  it('shows a passed-in field error next to the matching field in edit mode', () => {
    renderEditForm({ fieldErrors: { Title: ["'Title' must not be empty."] } })

    expect(screen.getByText("'Title' must not be empty.")).toBeInTheDocument()
  })

  it('renders a hidden currentIsCompleted field reflecting the todo prop', () => {
    renderEditForm({ todo: { ...editTodo, isCompleted: true } })

    expect(
      document.querySelector('input[name="currentIsCompleted"][value="true"]'),
    ).toBeInTheDocument()
  })
})
