import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TodoDetail } from '../../src/components/TodoDetail'
import type { TodoResponse } from '../../src/api/generated/models'

const todo: TodoResponse = {
  id: 'id-1',
  title: 'Buy milk',
  description: '2% or whole, whichever is on sale',
  dueDate: '2026-09-25T00:00:00Z',
  isCompleted: false,
  createdAt: '2026-09-19T14:32:07Z',
  updatedAt: '2026-09-20T09:00:00Z',
}

describe('TodoDetail', () => {
  it('renders all fields for a fully-populated todo', () => {
    render(<TodoDetail todo={todo} />)

    expect(screen.getByText('Buy milk')).toBeInTheDocument()
    expect(screen.getByText(todo.description!)).toBeInTheDocument()
    expect(screen.getByText(new Date(todo.dueDate!).toLocaleString())).toBeInTheDocument()
    expect(screen.getByText('Incomplete')).toBeInTheDocument()
    expect(screen.getByText(new Date(todo.createdAt).toLocaleString())).toBeInTheDocument()
    expect(screen.getByText(new Date(todo.updatedAt!).toLocaleString())).toBeInTheDocument()
  })

  it('renders placeholders when optional fields are null', () => {
    render(
      <TodoDetail
        todo={{ ...todo, description: null, dueDate: null, updatedAt: null }}
      />,
    )

    expect(screen.getByText('No description')).toBeInTheDocument()
    expect(screen.getByText('No due date')).toBeInTheDocument()
    expect(screen.getByText('Never updated')).toBeInTheDocument()
  })

  it('renders Completed status when the todo is done', () => {
    render(<TodoDetail todo={{ ...todo, isCompleted: true }} />)

    expect(screen.getByText('Completed')).toBeInTheDocument()
  })
})
